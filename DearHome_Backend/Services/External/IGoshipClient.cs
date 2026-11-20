using DearHome_Backend.DTOs.ShippingDtos;

namespace DearHome_Backend.Services.External;

/// <summary>
/// Typed HTTP client for the GoShip API: authentication (with distributed token caching), address lookups,
/// shipping rates and shipment creation. Request payloads are built by the shipping business logic.
/// </summary>
public interface IGoshipClient
{
    /// <summary>Returns a valid GoShip access token, from the distributed cache when possible.</summary>
    Task<string> GetAccessTokenAsync();

    /// <summary>GET cities; returns the response <c>data</c> element.</summary>
    Task<object> GetCitiesAsync();

    /// <summary>GET cities/{cityId}/districts; returns the response <c>data</c> element.</summary>
    Task<object> GetDistrictsAsync(string cityId);

    /// <summary>GET districts/{districtId}/wards; returns the response <c>data</c> element.</summary>
    Task<object> GetWardsAsync(string districtId);

    /// <summary>POST rates with the given payload; returns the response <c>data</c> list.</summary>
    Task<List<object>> GetRatesAsync(object ratesRequest);

    /// <summary>POST shipments with the given payload.</summary>
    Task<GoshipCreateShipmentResponse> CreateShipmentAsync(object shipmentRequest);
}
