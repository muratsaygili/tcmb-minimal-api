using System.ServiceModel;
using System.Text.Json;
using System.Web;
using System.Xml;
using Microsoft.OpenApi.Models;
using tcmb_api;
using tcmb_api.Models;
using WsBankaSubeOku;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TCMB Api", Version = "v1", Contact = new OpenApiContact() { Email = "muratsaygili1@gmail.com", Name = "Murat Saygýlý" } });
});

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Api v1");
        c.RoutePrefix = string.Empty;
    });
}

app.MapGet("/", () => "Hello World!");

app.MapGet("exchange-rate", (string currencyCodes) =>
{
    currencyCodes = string.IsNullOrEmpty(currencyCodes) ? "USD,EUR,GBP" : currencyCodes.ToUpper();
    /*
     * api key almak için https://evds2.tcmb.gov.tr/index.php?/evds/editProfile
     * adresine giriþ yapýlýr.
     */
    //get evds api key from appsettings.json
    var key = builder.Configuration["Evds2ApiKey"];

    var currencies = currencyCodes.Split(',').Select(currencyCode => new Currency() { CurrencyCode = currencyCode }).ToList();

    var seriesList = new List<string>();

    currencies.ForEach(currency =>
    {
        seriesList.Add($"TP.DK.{currency.CurrencyCode}.A");
        seriesList.Add($"TP.DK.{currency.CurrencyCode}.S");
        seriesList.Add($"TP.DK.{currency.CurrencyCode}.A.EF");
        seriesList.Add($"TP.DK.{currency.CurrencyCode}.S.EF");
    });



    var uriBuilder = new UriBuilder("https://evds2.tcmb.gov.tr/");
    var queryString = HttpUtility.ParseQueryString(uriBuilder.Query);
    queryString["series"] = string.Join("-", seriesList);
    queryString["startDate"] = DateTime.Now.AddDays(-10).ToString("dd-MM-yyyy");
    queryString["endDate"] = DateTime.Now.ToString("dd-MM-yyyy");
    queryString["type"] = "xml";
    queryString["key"] = key;
    uriBuilder.Path = string.Concat("service/evds/", queryString);

    var xml = new XmlDocument();
    xml.Load(new XmlTextReader(uriBuilder.ToString()));

    foreach (var currency in currencies)
    {
        currency.ForexBuying = xml.GetCurrencyValueFromXml($"TP_DK_{currency.CurrencyCode}_A");
        currency.ForexSelling = xml.GetCurrencyValueFromXml($"TP_DK_{currency.CurrencyCode}_S");
        currency.BanknoteBuying = xml.GetCurrencyValueFromXml($"TP_DK_{currency.CurrencyCode}_A_EF");
        currency.BanknoteSelling = xml.GetCurrencyValueFromXml($"TP_DK_{currency.CurrencyCode}_S_EF");


    }

    return currencies;
})
    .WithDescription("get exchange rates for Turkish Lira")
    .WithOpenApi();



app.MapGet("/bank-branch", async (httpContext) =>
{
    string json;
    TUMCvp? tumCevap;
    //check if file exists and created today
    if (File.Exists("tumCevap.json") && File.GetCreationTime("tumCevap.json").Date == DateTime.Now.Date)
    {
        json = await File.ReadAllTextAsync("tumCevap.json");
        tumCevap = JsonSerializer.Deserialize<TUMCvp>(json);
        await httpContext.Response.WriteAsJsonAsync(tumCevap.bankaSubeleri.Select(x => new
        {
            BankCode = x.bKd,
            BankName = x.banka.bAd,
            Branchs = x.sube.Select(x => new
            {
                BranchCode = x.sKd,
                BranchName = x.sAd
            })
        }).ToList());
        return;
    }


    var service = new bankaSubeOkuClient()
    {
        Endpoint =
        {
            Address = new EndpointAddress("https://appg.tcmb.gov.tr/mbnbasuse/services/bankaSubeOku"),
            Binding = new BasicHttpsBinding()
            {
                MaxReceivedMessageSize = 10000000
            }
        }
    };

    var response = await service.bankaSubeOkuAsync(new bankaSubeOkuGirdi("TUM", null, null));

    tumCevap = (TUMCvp)response.Item;

    //write tumCevap to json file
    json = JsonSerializer.Serialize(tumCevap);
    await File.WriteAllTextAsync("tumCevap.json", json);

    await httpContext.Response.WriteAsJsonAsync(tumCevap.bankaSubeleri.Select(x => new
    {
        BankCode = x.bKd,
        BankName = x.banka.bAd,
        Branchs = x.sube.Select(x => new
        {
            BranchCode = x.sKd,
            BranchName = x.sAd
        })
    }).ToList());
})
    .WithDescription("get bank and their branches of Turkey")
    .WithOpenApi(); ;

app.Run();
