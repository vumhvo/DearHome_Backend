using System.Reflection;
using DearHome_Backend.Constants;
using DearHome_Backend.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace DearHome_Backend.Tests.Authorization;

/// <summary>
/// Smoke tests over the controllers' authorization attributes (no HTTP pipeline): every endpoint must be either
/// explicitly anonymous or require authentication, and admin endpoints must require the Admin role.
/// </summary>
public class ControllerAuthorizationTests
{
    private const string Admin = nameof(UserRole.Admin);

    private static readonly Type[] Controllers = typeof(UserController).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
        .ToArray();

    private static IEnumerable<MethodInfo> Actions(Type controller) => controller
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

    private static MethodInfo Action(Type controller, string name) =>
        controller.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        ?? throw new InvalidOperationException($"{controller.Name}.{name} not found.");

    private static bool IsAnonymous(MethodInfo action) =>
        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

    private static IEnumerable<AuthorizeAttribute> AuthorizeAttributes(MethodInfo action) =>
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    /// <summary>
    /// Effective role requirement: ASP.NET Core combines [Authorize] attributes with AND, so an action is admin-only
    /// when any attribute on the action or its controller requires exactly the Admin role.
    /// </summary>
    private static bool RequiresAdmin(MethodInfo action) =>
        !IsAnonymous(action) && AuthorizeAttributes(action).Any(a => a.Roles == Admin);

    [Fact]
    public void Controllers_are_discovered()
    {
        Controllers.Should().HaveCountGreaterThanOrEqualTo(14);
    }

    [Fact]
    public void Every_action_is_either_AllowAnonymous_or_Authorize()
    {
        var unprotected = Controllers
            .SelectMany(Actions)
            .Where(a => !IsAnonymous(a) && !AuthorizeAttributes(a).Any())
            .Select(a => $"{a.DeclaringType!.Name}.{a.Name}")
            .ToList();

        unprotected.Should().BeEmpty("every endpoint must declare [AllowAnonymous] or be covered by [Authorize]");
    }

    [Fact]
    public void Role_names_in_Authorize_attributes_are_known_roles()
    {
        var knownRoles = Enum.GetNames<UserRole>();

        var roles = Controllers
            .SelectMany(c => c.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(Actions(c).SelectMany(a => a.GetCustomAttributes<AuthorizeAttribute>())))
            .Where(a => a.Roles != null)
            .SelectMany(a => a.Roles!.Split(',', StringSplitOptions.TrimEntries));

        roles.Should().OnlyContain(r => knownRoles.Contains(r));
    }

    [Theory]
    [InlineData(typeof(StatsController))]
    [InlineData(typeof(GoodReceivedNoteController))]
    public void Admin_only_controllers_require_Admin_on_every_action(Type controller)
    {
        controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be(Admin);
        Actions(controller).Should().NotBeEmpty().And.OnlyContain(a => RequiresAdmin(a));
    }

    [Theory]
    [InlineData(typeof(CategoryController))]
    [InlineData(typeof(AttributeController))]
    [InlineData(typeof(ProductController))]
    [InlineData(typeof(VariantController))]
    [InlineData(typeof(PlacementController))]
    public void Catalog_reads_are_public_and_writes_are_admin_only(Type controller)
    {
        foreach (var action in Actions(controller))
        {
            var isRead = action.GetCustomAttributes<HttpGetAttribute>().Any();
            if (isRead)
            {
                IsAnonymous(action).Should().BeTrue($"{controller.Name}.{action.Name} is a catalog read");
            }
            else
            {
                RequiresAdmin(action).Should().BeTrue($"{controller.Name}.{action.Name} modifies the catalog");
            }
        }
    }

    [Theory]
    [InlineData(typeof(OrderController), nameof(OrderController.GetAllOrders))]
    [InlineData(typeof(UserController), nameof(UserController.GetAllCustomers))]
    [InlineData(typeof(PromotionController), nameof(PromotionController.CreatePromotion))]
    [InlineData(typeof(PromotionController), nameof(PromotionController.UpdatePromotion))]
    [InlineData(typeof(ReviewController), nameof(ReviewController.Delete))]
    [InlineData(typeof(FirebaseStorageController), nameof(FirebaseStorageController.DeleteFile))]
    [InlineData(typeof(ShippingController), nameof(ShippingController.ValidateToken))]
    public void Admin_actions_on_mixed_controllers_require_Admin(Type controller, string action)
    {
        RequiresAdmin(Action(controller, action)).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(PaymentController), nameof(PaymentController.VerifyWebhook))]
    [InlineData(typeof(UserController), nameof(UserController.Login))]
    [InlineData(typeof(UserController), nameof(UserController.LoginWithGoogle))]
    [InlineData(typeof(UserController), nameof(UserController.Register))]
    [InlineData(typeof(UserController), nameof(UserController.SendVerificationCode))]
    public void Public_endpoints_are_AllowAnonymous(Type controller, string action)
    {
        IsAnonymous(Action(controller, action)).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(OrderController), nameof(OrderController.CreateOrder))]
    [InlineData(typeof(PaymentController), nameof(PaymentController.VerifyPayment))]
    [InlineData(typeof(UserController), nameof(UserController.GetCurrentUser))]
    [InlineData(typeof(UserController), nameof(UserController.ChangePassword))]
    [InlineData(typeof(ReviewController), nameof(ReviewController.Create))]
    public void Customer_endpoints_require_authentication_but_not_Admin(Type controller, string action)
    {
        var method = Action(controller, action);

        IsAnonymous(method).Should().BeFalse();
        AuthorizeAttributes(method).Should().NotBeEmpty();
        RequiresAdmin(method).Should().BeFalse();
    }
}
