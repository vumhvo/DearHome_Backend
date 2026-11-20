using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DearHome_Backend.DTOs.PaginationDtos;
using DearHome_Backend.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DearHome_Backend.Repositories.Implementations
{
    /// <summary>
    /// Base repository class that implements common CRUD operations.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    /// <remarks>
    /// This class provides a generic implementation for basic CRUD operations.
    /// </remarks>
    public class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        protected readonly DbContext _context;

        public BaseRepository(DbContext context)
        {
            _context = context;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public virtual async Task<PaginatedResult<T>> GetAllAsync(int offSet, int limit, string? search = null)
        {
            (offSet, limit) = NormalizePaging(offSet, limit);
            var query = _context.Set<T>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Assuming T has a property called "Name" for search purposes
                query = query.Where(e => EF.Property<string>(e, "Name").Contains(search));
            }

            return new PaginatedResult<T>
            {
                Data = await query.Skip(offSet).Take(limit).ToListAsync(),
                PageNumber = offSet / limit + 1,
                PageSize = limit,
                TotalRecords = await query.CountAsync()
            };
        }

        public virtual async Task<PaginatedResult<T>> GetAllAsync(int offSet, int limit, string? search = null, string? filter = null, string? sortBy = null, bool isDescending = false)
        {
            (offSet, limit) = NormalizePaging(offSet, limit);
            var query = _context.Set<T>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => EF.Property<string>(e, "Name").Contains(search));
            }

            // filter = "only rows where this property is not null"; unknown property names are ignored.
            var filterProperty = FindMappedProperty(filter);
            if (filterProperty != null)
            {
                var notNull = BuildNotNullPredicate(filterProperty);
                if (notNull != null)
                {
                    query = query.Where(notNull);
                }
            }

            query = ApplySorting(query, sortBy, isDescending);

            return new PaginatedResult<T>
            {
                Data = await query.Skip(offSet).Take(limit).ToListAsync(),
                PageNumber = offSet / limit + 1,
                PageSize = limit,
                TotalRecords = await query.CountAsync()
            };
        }

        /// <summary>Default page size when the caller does not supply a usable limit.</summary>
        protected const int DefaultPageSize = 10;

        /// <summary>Largest page size a caller can request.</summary>
        protected const int MaxPageSize = 100;

        /// <summary>
        /// Makes client-supplied paging safe: a limit below 1 becomes <see cref="DefaultPageSize"/>, a limit above
        /// <see cref="MaxPageSize"/> is capped, and a negative offset becomes 0.
        /// </summary>
        protected static (int OffSet, int Limit) NormalizePaging(int offSet, int limit)
        {
            if (limit < 1)
            {
                limit = DefaultPageSize;
            }
            else if (limit > MaxPageSize)
            {
                limit = MaxPageSize;
            }

            return (Math.Max(0, offSet), limit);
        }

        /// <summary>
        /// Resolves a client-supplied property name (case-insensitive) to a scalar property mapped on <typeparamref name="T"/>.
        /// Returns null for empty or unknown names so they can be ignored.
        /// </summary>
        protected IProperty? FindMappedProperty(string? propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var name = propertyName.Trim();
            var entityType = _context.Model.FindEntityType(typeof(T));
            if (entityType == null)
            {
                return null;
            }

            var property = entityType.FindProperty(name)
                ?? entityType.GetProperties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            // Primitive collections are stored as JSON; they are not meaningful to sort or filter on.
            return property is { IsPrimitiveCollection: false } ? property : null;
        }

        /// <summary>
        /// Orders by a mapped property of <typeparamref name="T"/>. Unknown or empty <paramref name="sortBy"/> values
        /// leave the query unchanged.
        /// </summary>
        protected IQueryable<T> ApplySorting(IQueryable<T> query, string? sortBy, bool isDescending)
        {
            var property = FindMappedProperty(sortBy);
            if (property == null)
            {
                return query;
            }

            var propertyName = property.Name;
            return isDescending
                ? query.OrderByDescending(e => EF.Property<object>(e, propertyName))
                : query.OrderBy(e => EF.Property<object>(e, propertyName));
        }

        /// <summary>
        /// Builds <c>e => EF.Property&lt;TProp&gt;(e, name) != null</c>. Returns null for non-nullable value types,
        /// which can never be null (so no filtering is needed).
        /// </summary>
        private static Expression<Func<T, bool>>? BuildNotNullPredicate(IProperty property)
        {
            var clrType = property.ClrType;
            if (clrType.IsValueType && Nullable.GetUnderlyingType(clrType) == null)
            {
                return null;
            }

            var parameter = Expression.Parameter(typeof(T), "e");
            var propertyAccess = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                new[] { clrType },
                parameter,
                Expression.Constant(property.Name));
            var body = Expression.NotEqual(propertyAccess, Expression.Constant(null, clrType));
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        public virtual async Task<T?> GetByIdAsync(object id)
        {
            return await _context.Set<T>().FindAsync(id);
        }

        public virtual async Task AddAsync(T entity)
        {
            await _context.Set<T>().AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task UpdateAsync(T entity)
        {
            var trackedEntity = _context.Set<T>().Local.FirstOrDefault(e =>
            {
                var eId = _context.Entry(e).Property("Id").CurrentValue;
                var entityId = _context.Entry(entity).Property("Id").CurrentValue;
                return eId != null && entityId != null && eId.Equals(entityId);
            });
            if (trackedEntity != null)
            {
                _context.Entry(trackedEntity).State = EntityState.Detached;
            }
            _context.Set<T>().Update(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(object id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _context.Set<T>().Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public IQueryable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _context.Set<T>().Where(predicate);
        }

        public Task DeleteRangeAsync(IEnumerable<T> entities)
        {
           return Task.Run(() =>
            {
                _context.Set<T>().RemoveRange(entities);
                _context.SaveChanges();
            });
        }

        public Task DeleteRangeByIdsAsync(IEnumerable<object> ids)
        {
            return Task.Run(() =>
            {
                var entities = _context.Set<T>().Where(e => ids.Contains(_context.Entry(e).Property("Id").CurrentValue));
                _context.Set<T>().RemoveRange(entities);
                _context.SaveChanges();
            });
        }
    }
}
