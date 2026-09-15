var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "MediRoute API Gateway", Version = "v1" }));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapGet("/", () => Results.Ok(new
{
    service = "MediRoute API Gateway",
    version = "1.0",
    endpoints = new
    {
        triage = "/api/triage",
        hospitals = "/api/hospitals",
        routing = "/api/routing",
        notifications = "/api/notifications",
        auth = "/api/auth",
        ambulances = "/api/ambulances",
        emergencies = "/api/emergencies",
        signalr = "/hubs/emergency"
    }
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ApiGateway" }));
app.MapReverseProxy();

app.Run();
