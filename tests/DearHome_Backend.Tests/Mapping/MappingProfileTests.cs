using AutoMapper;
using AutoMapper.Internal;
using DearHome_Backend.DTOs.AddressDtos;
using DearHome_Backend.DTOs.AttributeDtos;
using DearHome_Backend.DTOs.AttributeValueDtos;
using DearHome_Backend.DTOs.CategoryDtos;
using DearHome_Backend.DTOs.GoodReceivedItemDtos;
using DearHome_Backend.DTOs.GoodReceivedNoteDtos;
using DearHome_Backend.DTOs.OrderDetailDtos;
using DearHome_Backend.DTOs.OrderDtos;
using DearHome_Backend.DTOs.PlacementDtos;
using DearHome_Backend.DTOs.ProductDtos;
using DearHome_Backend.DTOs.PromotionDtos;
using DearHome_Backend.DTOs.ReviewDtos;
using DearHome_Backend.DTOs.UserDtos;
using DearHome_Backend.DTOs.VariantAttributeDtos;
using DearHome_Backend.DTOs.VariantDtos;
using DearHome_Backend.Helpers;
using DearHome_Backend.Models;

namespace DearHome_Backend.Tests.Mapping;

public class MappingProfileTests
{
    private static readonly MapperConfiguration Configuration = new(cfg => cfg.AddProfile<MappingProfile>());
    private readonly IMapper _mapper = Configuration.CreateMapper();

    // AssertConfigurationIsValid() fails on purpose: the DTO -> entity maps (Add*/Update*Dto -> entity) leave
    // Id, CreatedAt, UpdatedAt and navigation properties unmapped by design. Instead, every entity -> DTO map (what
    // the API returns) is checked for DTO members that AutoMapper can never fill.
    private static IEnumerable<string> UnmappedDtoMembers(Type source, Type destination)
    {
        var typeMap = Configuration.Internal().FindTypeMapFor(source, destination);
        typeMap.Should().NotBeNull($"MappingProfile should map {source.Name} -> {destination.Name}");
        return typeMap!.GetUnmappedPropertyNames();
    }

    [Theory]
    [InlineData(typeof(User), typeof(UserDto))]
    [InlineData(typeof(User), typeof(ReviewerDto))]
    [InlineData(typeof(Address), typeof(AddressDto))]
    [InlineData(typeof(Category), typeof(CategoryDto))]
    [InlineData(typeof(Models.Attribute), typeof(AttributeDto))]
    [InlineData(typeof(Placement), typeof(PlacementDto))]
    [InlineData(typeof(AttributeValue), typeof(AttributeValueDto))]
    [InlineData(typeof(Product), typeof(ProductDto))]
    [InlineData(typeof(Variant), typeof(VariantDto))]
    [InlineData(typeof(VariantAttribute), typeof(VariantAttributeDto))]
    [InlineData(typeof(GoodReceivedNote), typeof(GoodReceivedNoteDto))]
    [InlineData(typeof(GoodReceivedItem), typeof(GoodReceivedItemDto))]
    [InlineData(typeof(Order), typeof(OrderDto))]
    [InlineData(typeof(Order), typeof(ProductReviewOrderDto))]
    [InlineData(typeof(OrderDetail), typeof(OrderDetailDto))]
    [InlineData(typeof(OrderDetail), typeof(ProductReviewOrderDetailDto))]
    [InlineData(typeof(Promotion), typeof(PromotionDto))]
    [InlineData(typeof(Review), typeof(ReviewSummaryDto))]
    [InlineData(typeof(Review), typeof(ProductReviewDto))]
    public void Entity_to_dto_map_fills_every_dto_member(Type entity, Type dto)
    {
        UnmappedDtoMembers(entity, dto).Should().BeEmpty();
    }

    [Fact]
    public void CategoryAttribute_to_dto_leaves_AttributeValues_empty()
    {
        // CategoryAttribute has no AttributeValues; the DTO keeps its initializer (an empty list). Values are exposed
        // through Attribute.AttributeValues instead.
        UnmappedDtoMembers(typeof(CategoryAttribute), typeof(DTOs.CategoryAttributeDtos.CategoryAttributeDto))
            .Should().BeEquivalentTo("AttributeValues");
    }

    [Fact]
    public void Review_to_ReviewDto_currently_leaves_variant_user_and_order_unmapped()
    {
        // BUG: ReviewDto declares VariantId/Variant, UserId/User and OrderId/Order, but Review only has
        // OrderDetailId/OrderDetail, so GET /api/Review responses always carry empty GUIDs and nulls for them.
        UnmappedDtoMembers(typeof(Review), typeof(ReviewDto))
            .Should().BeEquivalentTo("VariantId", "Variant", "UserId", "User", "OrderId", "Order");
    }

    [Fact(Skip = "BUG: Review -> ReviewDto never fills VariantId/Variant, UserId/User, OrderId/Order (Review only has OrderDetail).")]
    public void Review_to_ReviewDto_fills_every_dto_member()
    {
        UnmappedDtoMembers(typeof(Review), typeof(ReviewDto)).Should().BeEmpty();
    }

    [Fact]
    public void User_to_UserDto_copies_profile_fields()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "jane",
            Email = "jane@example.com",
            Name = "Jane Doe",
            PhoneNumber = "0900000000",
            ImageUrl = "https://cdn.test/jane.png",
            CustomerPoints = 42,
            PasswordHash = "secret-hash",
            Addresses = new List<Address> { new() { Id = Guid.NewGuid(), City = "Hà Nội", UserId = Guid.NewGuid() } }
        };

        var dto = _mapper.Map<UserDto>(user);

        dto.Id.Should().Be(user.Id);
        dto.UserName.Should().Be("jane");
        dto.Email.Should().Be("jane@example.com");
        dto.Name.Should().Be("Jane Doe");
        dto.PhoneNumber.Should().Be("0900000000");
        dto.CustomerPoints.Should().Be(42);
        dto.Addresses.Should().ContainSingle().Which.City.Should().Be("Hà Nội");
    }

    [Fact]
    public void UserDto_does_not_expose_credential_or_security_fields()
    {
        var forbidden = new[]
        {
            nameof(User.PasswordHash), nameof(User.SecurityStamp), nameof(User.ConcurrencyStamp),
            nameof(User.NormalizedEmail), nameof(User.NormalizedUserName), nameof(User.AccessFailedCount),
            nameof(User.LockoutEnd), nameof(User.TwoFactorEnabled)
        };

        typeof(UserDto).GetProperties().Select(p => p.Name).Should().NotContain(forbidden);
    }

    [Fact]
    public void Order_to_OrderDto_copies_prices()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AddressId = Guid.NewGuid(),
            TotalPrice = 290m,
            Discount = 29m,
            FinalPrice = 291m
        };

        var dto = _mapper.Map<OrderDto>(order);

        dto.Id.Should().Be(order.Id);
        dto.TotalPrice.Should().Be(290m);
        dto.Discount.Should().Be(29m);
        dto.FinalPrice.Should().Be(291m);
    }
}
