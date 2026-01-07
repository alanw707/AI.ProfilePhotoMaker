using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class CreditControllerPaymentConfigTests
{
    private readonly Mock<ICreditPackageService> _creditPackageService = new();
    private readonly Mock<IBasicTierService> _basicTierService = new();
    private readonly Mock<IStripePaymentService> _stripePaymentService = new();
    private readonly Mock<IWebHostEnvironment> _environment = new();
    private readonly Mock<ILogger<CreditController>> _logger = new();

    [Theory]
    [MemberData(nameof(GetPaymentConfigScenarios))]
    public void GetPaymentConfig_ReturnsExpectedSimulationFlags(
        StripeOptions stripeOptions,
        PaymentSimulationOptions simulationOptions,
        bool expectedSimulationEnabled,
        string? expectedReason)
    {
        var controller = CreateController(stripeOptions, simulationOptions);

        var actionResult = controller.GetPaymentConfig();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var envelope = okResult.Value!.ToDictionary();

        Assert.True((bool)envelope["success"]!);
        var data = ((object?)envelope["data"]!).ToDictionary();

        var simulation = ((object?)data["paymentSimulation"]!).ToDictionary();
        Assert.Equal(expectedSimulationEnabled, simulation["enabled"]);
        Assert.Equal(expectedSimulationEnabled, simulation["skipStripeIntegration"]);
        Assert.Equal(expectedReason, simulation["reason"]);

        var stripe = ((object?)data["stripe"]!).ToDictionary();
        var stripeReady = stripeOptions.HasApiKeys() && stripeOptions.HasWebhookSecret();
        var simulationForced = simulationOptions.Enabled && simulationOptions.SkipStripeIntegration;
        Assert.Equal(stripeReady && !simulationForced, stripe["enabled"]);
        Assert.Equal(stripeReady ? stripeOptions.PublishableKey : null, stripe["publishableKey"]);
        Assert.Equal(stripeOptions.HasWebhookSecret(), stripe["webhookConfigured"]);
    }

    [Fact]
    public void GetPaymentConfig_BlocksLiveKeysInDevelopmentByDefault()
    {
        var controller = CreateController(
            new StripeOptions
            {
                PublishableKey = "pk_live_test",
                SecretKey = "sk_live_test",
                WebhookSecret = "whsec_live_test"
            },
            new PaymentSimulationOptions { Enabled = false, SkipStripeIntegration = false },
            Environments.Development);

        var actionResult = controller.GetPaymentConfig();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var envelope = okResult.Value!.ToDictionary();
        var data = ((object?)envelope["data"]!).ToDictionary();
        var simulation = ((object?)data["paymentSimulation"]!).ToDictionary();
        var stripe = ((object?)data["stripe"]!).ToDictionary();

        Assert.True((bool)simulation["enabled"]!);
        Assert.True((bool)simulation["skipStripeIntegration"]!);
        Assert.Equal("StripeLiveKeysInDevelopment", simulation["reason"]);
        Assert.False((bool)stripe["enabled"]!);
        Assert.Null(stripe["publishableKey"]);
        Assert.False((bool)stripe["webhookConfigured"]!);
    }

    public static IEnumerable<object?[]> GetPaymentConfigScenarios()
    {
        yield return new object?[]
        {
            new StripeOptions { PublishableKey = null!, SecretKey = null!, WebhookSecret = null! },
            new PaymentSimulationOptions { Enabled = false, SkipStripeIntegration = false },
            true,
            "StripeNotConfigured"
        };

        yield return new object?[]
        {
            new StripeOptions { PublishableKey = "pk_test", SecretKey = "sk_test", WebhookSecret = "whsec_test" },
            new PaymentSimulationOptions { Enabled = false, SkipStripeIntegration = false },
            false,
            null
        };

        yield return new object?[]
        {
            new StripeOptions { PublishableKey = "pk_test", SecretKey = "sk_test", WebhookSecret = null! },
            new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true },
            true,
            "WebhookNotConfigured"
        };

        yield return new object?[]
        {
            new StripeOptions { PublishableKey = "pk_test", SecretKey = "sk_test", WebhookSecret = "whsec_test" },
            new PaymentSimulationOptions { Enabled = true, SkipStripeIntegration = true },
            true,
            "ExplicitBypass"
        };
    }

    private CreditController CreateController(
        StripeOptions stripeOptions,
        PaymentSimulationOptions simulationOptions,
        string environmentName = "Production")
    {
        _environment.SetupGet(env => env.EnvironmentName).Returns(environmentName);

        var controller = new CreditController(
            _creditPackageService.Object,
            _basicTierService.Object,
            _stripePaymentService.Object,
            Options.Create(stripeOptions),
            Options.Create(simulationOptions),
            _environment.Object,
            _logger.Object)
        {
            ControllerContext = ControllerContextFactory.CreateWithUser("test-user")
        };

        return controller;
    }
}

internal static class ControllerContextFactory
{
    public static ControllerContext CreateWithUser(string userId)
    {
        var context = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        context.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth"));

        return context;
    }
}

internal static class ObjectExtensions
{
    public static Dictionary<string, object?> ToDictionary(this object? value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value is IDictionary<string, object?> typedDictionary)
        {
            return new Dictionary<string, object?>(typedDictionary);
        }

        if (value is IDictionary<string, object> nonNullableDictionary)
        {
            return nonNullableDictionary.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        }

        var result = new Dictionary<string, object?>();

        foreach (var property in value.GetType().GetProperties())
        {
            var propertyName = property.Name.Length switch
            {
                0 => string.Empty,
                1 => property.Name.ToLowerInvariant(),
                _ => char.ToLowerInvariant(property.Name[0]) + property.Name[1..]
            };

            result[propertyName] = property.GetValue(value);
        }

        return result;
    }
}
