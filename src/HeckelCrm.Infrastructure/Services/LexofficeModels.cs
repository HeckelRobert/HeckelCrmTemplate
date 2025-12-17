using System.Text.Json.Serialization;

namespace HeckelCrm.Infrastructure.Services;

// Lexoffice API Models based on https://developers.lexware.io/docs/#contacts-endpoint-create-a-contact

public class LexofficeContactRequest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 0;

    [JsonPropertyName("roles")]
    public LexofficeContactRoles Roles { get; set; } = new();

    [JsonPropertyName("person")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LexofficePerson? Person { get; set; }

    [JsonPropertyName("company")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LexofficeCompany? Company { get; set; }

    [JsonPropertyName("addresses")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LexofficeAddresses? Addresses { get; set; }

    [JsonPropertyName("emailAddresses")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<string>>? EmailAddresses { get; set; }

    [JsonPropertyName("phoneNumbers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<string>>? PhoneNumbers { get; set; }

    [JsonPropertyName("note")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }

    [JsonPropertyName("archived")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Archived { get; set; }
}

public class LexofficeContactRoles
{
    [JsonPropertyName("customer")]
    public LexofficeCustomerRole Customer { get; set; } = new();
}

public class LexofficeCustomerRole
{
    // Empty object as per API documentation
}

public class LexofficePerson
{
    [JsonPropertyName("salutation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Salutation { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
}

public class LexofficeCompany
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("taxNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TaxNumber { get; set; }

    [JsonPropertyName("vatRegistrationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VatRegistrationId { get; set; }

    [JsonPropertyName("allowTaxFreeInvoices")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowTaxFreeInvoices { get; set; }

    [JsonPropertyName("contactPersons")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LexofficeContactPerson>? ContactPersons { get; set; }
}

public class LexofficeContactPerson
{
    [JsonPropertyName("salutation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Salutation { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("emailAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("phoneNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhoneNumber { get; set; }
}

public class LexofficeAddresses
{
    [JsonPropertyName("billing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LexofficeAddress>? Billing { get; set; }

    [JsonPropertyName("shipping")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LexofficeAddress>? Shipping { get; set; }
}

public class LexofficeAddress
{
    [JsonPropertyName("supplement")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Supplement { get; set; }

    [JsonPropertyName("street")]
    public string Street { get; set; } = string.Empty;

    [JsonPropertyName("zip")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Zip { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = "DE";
}

public class LexofficeContactResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("roles")]
    public LexofficeContactRoles Roles { get; set; } = new();

    [JsonPropertyName("person")]
    public LexofficePerson? Person { get; set; }

    [JsonPropertyName("company")]
    public LexofficeCompany? Company { get; set; }

    [JsonPropertyName("addresses")]
    public LexofficeAddresses? Addresses { get; set; }

    [JsonPropertyName("emailAddresses")]
    public Dictionary<string, List<string>>? EmailAddresses { get; set; }

    [JsonPropertyName("phoneNumbers")]
    public Dictionary<string, List<string>>? PhoneNumbers { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }
}

