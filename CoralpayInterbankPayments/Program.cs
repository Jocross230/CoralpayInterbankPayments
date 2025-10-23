using CoralPayInterbankPayment.Interface;
using CoralpayInterbankPayments.Interface;
using CoralPayInterbankPayment.Model;
using CoralPayInterbankPayment.Service;
using CoralPayInterbankPayment.Data;
using Microsoft.EntityFrameworkCore;
using CoralpayInterbankPayments.Service;
using CoralpayInterbankPayments.Model;

var builder = WebApplication.CreateBuilder(args);

var connection = builder.Configuration.GetConnectionString("TestDB") 
                 ?? throw new InvalidOperationException("Missing connection string 'TestDB'.");
builder.Services.AddDbContext<CreditDbContext>(options =>
    options.UseSqlServer(connection));

builder.Services.Configure<AccountCodeSettings>(builder.Configuration.GetSection("AccountCodeSettings"));
builder.Services.Configure<EndpointSettings>(builder.Configuration.GetSection("EndpointSettings"));
builder.Services.Configure<OneNumBaAuth>(builder.Configuration.GetSection("OneNumBaAuth"));

var configurations = builder.Configuration;
SunTrustProxy.AppId = configurations.GetValue<string>("AppSettings:AppId");
SunTrustProxy.InstitutionCode = configurations.GetValue<string>("AppSettings:InstitutionCode");
SunTrustProxy.MiddlewareBaseUrl = configurations.GetValue<string>("AppSettings:MiddleWareUrl");
SunTrustProxy.StbServiceBaseUrl = configurations.GetValue<string>("AppSettings:Stbservice");
SunTrustProxy.AppPassword = configurations.GetValue<string>("AppSettings:AppPassword");

builder.Services.AddHttpClient<ICipIncomingService, CipIncomingService>();
builder.Services.AddHttpClient<ITsqService, TsqService>();
builder.Services.AddHostedService<TsqWorker>();
builder.Services.AddSingleton<PgpWrapperService>();

builder.Services.AddControllers(options =>
{
    options.InputFormatters.Insert(0, new TextPlainInputFormatter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigins", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseSwagger();
app.UseMiddleware<BasicAuthMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CoralPayInterbankPayments v1");
    });
}
else
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/CoralPayInterbankPayments/swagger/v1/swagger.json", "CoralPayInterbankPayments v1");
    });
}

app.UseCors("AllowAnyOrigins");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
