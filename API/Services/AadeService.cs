using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using API.DTOs.Customer;

public class AadeService(IHttpClientFactory httpClientFactory, IConfiguration config)
{
    // Τα ακριβή Namespaces που απαιτεί η ΑΑΔΕ για SOAP 1.2
    private static readonly XNamespace Env = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Ns1 = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Ns2 = "http://rgwspublic2/RgWsPublic2Service";
    private static readonly XNamespace Ns3 = "http://rgwspublic2/RgWsPublic2";

    public async Task<AadeCompanyDto?> LookupByAfmAsync(string afm)
    {
        var url        = config["Aade:Url"]!;
        var username   = config["Aade:Username"]!;
        var password   = config["Aade:Password"]!;
        var callerAfm  = config["Aade:CallerAfm"]!;

        // Κατασκευή του αυστηρού SOAP Envelope
        var envelope = new XDocument(
            new XElement(Env + "Envelope",
                new XAttribute(XNamespace.Xmlns + "env", Env.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ns1", Ns1.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ns2", Ns2.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ns3", Ns3.NamespaceName),
                
                new XElement(Env + "Header",
                    new XElement(Ns1 + "Security",
                        new XElement(Ns1 + "UsernameToken",
                            new XElement(Ns1 + "Username", username),
                            new XElement(Ns1 + "Password", password)
                        )
                    )
                ),
                new XElement(Env + "Body",
                    new XElement(Ns2 + "rgWsPublic2AfmMethod",
                        new XElement(Ns2 + "INPUT_REC", 
                            new XElement(Ns3 + "afm_called_by", callerAfm),
                            new XElement(Ns3 + "afm_called_for", afm)
                        )
                    )
                )
            )
        );

        var client = httpClientFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            // To SOAP 1.2 απαιτεί application/soap+xml
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "application/soap+xml")
        };

        var response = await client.SendAsync(request);
        var xml = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"ΑΑΔΕ error {response.StatusCode}: {xml}");

        return ParseResponse(afm, xml);
    }

    private static AadeCompanyDto? ParseResponse(string afm, string xml)
    {
        var doc = XDocument.Parse(xml);

        // Έλεγχος για business σφάλμα (π.χ. λάθος κωδικοί ή μη έγκυρο ΑΦΜ)
        var error = doc.Descendants(Ns3 + "error_descr").FirstOrDefault()?.Value;
        if (!string.IsNullOrWhiteSpace(error) && error != "OK")
            throw new Exception($"ΑΑΔΕ: {error}");

        // Η ΑΑΔΕ επιστρέφει τα δεδομένα μέσα στο basic_rec
        var basic = doc.Descendants(Ns3 + "basic_rec").FirstOrDefault();
        if (basic is null) return null;

        var deactivated = basic.Element(Ns3 + "deactivation_flag")?.Value;

        return new AadeCompanyDto
        {
            Afm            = afm,
            Name           = basic.Element(Ns3 + "onomasia")?.Value,
            NameEn         = basic.Element(Ns3 + "onomasia_en")?.Value,
            Doy            = basic.Element(Ns3 + "doy")?.Value,
            DoyDescription = basic.Element(Ns3 + "doy_descr")?.Value,
            Address        = basic.Element(Ns3 + "postal_address")?.Value,
            AddressNo      = basic.Element(Ns3 + "postal_address_no")?.Value,
            ZipCode        = basic.Element(Ns3 + "postal_zip_code")?.Value,
            City           = basic.Element(Ns3 + "postal_area_description")?.Value,
            CompanyType    = basic.Element(Ns3 + "firmFlagDescr")?.Value,
            // Το deactivation_flag επιστρέφει "1" για ενεργό ΑΦΜ
            IsActive       = deactivated == "1", 
        };
    }
}