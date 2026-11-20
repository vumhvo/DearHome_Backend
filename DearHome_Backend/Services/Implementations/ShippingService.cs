using System.Text.Json;
using DearHome_Backend.DTOs.ShippingDtos;
using DearHome_Backend.Repositories.Interfaces;
using DearHome_Backend.Services.External;
using DearHome_Backend.Services.Interfaces;

namespace DearHome_Backend.Services.Implementations;

/// <summary>
/// Shipping business logic (payload building, address formatting). Raw GoShip HTTP calls and token caching live in
/// <see cref="IGoshipClient"/>.
/// </summary>
public class ShippingService : IShippingService
{
    // Warehouse (sender) location used for every rate quote and shipment.
    private const string WarehouseCityId = "700000";
    private const string WarehouseDistrictId = "700700";

    private readonly IAddressRepository _addressRepository;
    private readonly IGoshipClient _goshipClient;

    public ShippingService(IAddressRepository addressRepository, IGoshipClient goshipClient)
    {
        _addressRepository = addressRepository ?? throw new ArgumentNullException(nameof(addressRepository));
        _goshipClient = goshipClient ?? throw new ArgumentNullException(nameof(goshipClient));
    }

    public Task<string> GetValidationTokenAsync() => _goshipClient.GetAccessTokenAsync();

    public Task<bool> ValidateShippingAddressAsync(Guid addressId)
    {
        // Implementation for validating shipping address
        throw new NotImplementedException();
    }

    public Task<string> GetShippingCarrierAsync(Guid addressId)
    {
        // Implementation for getting shipping carrier
        throw new NotImplementedException();
    }

    public Task<string> TrackShipmentAsync(string trackingNumber)
    {
        // Implementation for tracking shipment
        throw new NotImplementedException();
    }

    public Task<bool> UpdateShippingStatusAsync(Guid orderId, string status)
    {
        // Implementation for updating shipping status
        throw new NotImplementedException();
    }

    public async Task<object> CalculateShippingCostAsync(Guid addressId, Guid addressFromId, string cod, string weight, string length, string width, string height)
    {
        //Send request to GoShip API for shipping cost calculation
        var address = await _addressRepository.GetByIdAsync(addressId);
        if (address == null)
        {
            throw new Exception("Address not found");
        }
        var cityId = address.City;
        var districtId = address.District;

        var request = new
        {
            shipment = new
            {
                address_from = new
                {
                    city = WarehouseCityId,
                    district = WarehouseDistrictId,
                },
                address_to = new
                {
                    city = cityId,
                    district = districtId,
                },
                parcel = new
                {
                    code = cod,
                    width = width,
                    length = length,
                    weight = weight,
                    height = height,
                },
            }
        };

        return await _goshipClient.GetRatesAsync(request);
    }

    public Task<object> GetDistrictsByCityIdAsync(string cityId) => _goshipClient.GetDistrictsAsync(cityId);

    public Task<object> GetCitiesAsync() => _goshipClient.GetCitiesAsync();

    public async Task<GoshipCreateShipmentResponse> CreateShipmentAsync(string shippingRate, Guid addressFromId, Guid addressToId, string cod, string weight, string length, string width, string height)
    {
        var address = await _addressRepository.GetByIdAsync(addressToId);
        if (address == null)
        {
            throw new Exception("Address not found");
        }
        var cityId = address.City;
        var districtId = address.District;

        var request = new
        {
            shipment = new
            {
                rate = shippingRate,
                address_from = new
                {
                    name = "Dear Home",
                    phone = "123456789",
                    street = "123 Main St",
                    ward = "9233",
                    city = WarehouseCityId,
                    district = WarehouseDistrictId,

                },
                address_to = new
                {
                    name = "Customer Name",
                    phone = "123456789",
                    street = address.Street,
                    ward = "9020",
                    city = cityId,
                    district = districtId,
                },
                parcel = new
                {
                    cod = cod,
                    width = width,
                    length = length,
                    weight = weight,
                    height = height,
                    metadata = "metadata",
                },
            }
        };

        return await _goshipClient.CreateShipmentAsync(request);
    }

    public Task<object> GetWardsByDistrictIdAsync(string districtId) => _goshipClient.GetWardsAsync(districtId);

    public async Task<object> GetCitiesByCodeAsync(string code)
    {
        object cities = await GetCitiesAsync();
        
        // Handle JsonElement properly
        var citiesElement = (JsonElement)cities;
        JsonElement? foundCity = null;
        
        if (citiesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var city in citiesElement.EnumerateArray())
            {
                if (city.TryGetProperty("id", out var idProperty) && 
                    idProperty.GetString() == code)
                {
                    foundCity = city;
                    break;
                }
            }
        }
        
        if (foundCity == null)
        {
            throw new Exception("City not found");
        }
        
        return foundCity.Value;
    }

    public async Task<object> GetFormatedAddressAsync(Guid addressId)
    {
        var address = await _addressRepository.GetByIdAsync(addressId);
        if (address == null)
        {
            throw new Exception("Address not found");
        }
        var city = await GetCitiesByCodeAsync(address.City!);

        var districts = await GetDistrictsByCityIdAsync(address.City!);
        var district = ((JsonElement)districts).EnumerateArray()
            .FirstOrDefault(d => d.GetProperty("id").GetString() == address.District);

        var wards = await GetWardsByDistrictIdAsync(address.District!);
        var ward = ((JsonElement)wards).EnumerateArray()
            .FirstOrDefault(w => w.GetProperty("id").ValueKind == JsonValueKind.Number 
                ? w.GetProperty("id").GetInt64().ToString() == address.Ward
                : w.GetProperty("id").GetString() == address.Ward);

        var formatedAddress = new
        {
            id = address.Id,
            address.Street,
            city,
            district,
            ward, 
            address.PostalCode,   
            address.Country,
        };
        return formatedAddress;
    }
}
