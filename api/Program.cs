using CareLanka.Api.Common.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCareLankaPersistence(builder.Configuration)
    .AddCareLankaAuth(builder.Configuration)
    .AddCareLankaWebApi()
    .AddComponentServices();

// Dev only. In production the React app is served from the same origin as the API and
// there is no cross-origin request to allow.
const string DevCors = "carelanka-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("http://localhost:5173", "http://localhost:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// First in the pipeline: nothing above it can throw an unshaped error.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCors);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
