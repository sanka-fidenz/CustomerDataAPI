using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});
builder.Services.AddAuthorization();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "CustomerData";
    config.Title = "CustomerData v1";
    config.Version = "v1";
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "CustomerData";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

app.MapGet("/", () => "API works!");

app.MapPost("/authenticate", (LoginModel login, IConfiguration configuration) =>
{
    if (login.Username == "username" && login.Password == "password")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, login.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: creds);

        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
    }

    return Results.Unauthorized();
});

app.MapGet("/users", async (string? key, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(key))
    {
        return await db.Users.Include(u => u.Address).ToListAsync();
    }
    return await db.Users.Where(u => u.Name.ToUpper().Contains(key.ToUpper())).Include(u => u.Address).ToListAsync();
}).RequireAuthorization();

app.MapGet("/users-group-by-zip-code", async (AppDbContext db) =>
{
    return await db.Users.Include(u => u.Address).GroupBy(u => u.Address.Zipcode).ToListAsync();
}).RequireAuthorization();

app.MapPost("/users", async (User user, AppDbContext db) =>
{

    if (string.IsNullOrEmpty(user.Id))
    {
        user.Id = Guid.NewGuid().ToString();
    }

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", user);
}).RequireAuthorization();

app.MapPut("/users/{id}", async (string id, User inputUser, AppDbContext db) =>
{
    var todo = await db.Users.FindAsync(id);

    if (todo is null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(inputUser.Name))
    {
        todo.Name = inputUser.Name;
    }
    if (!string.IsNullOrWhiteSpace(inputUser.Email))
    {
        todo.Email = inputUser.Email;
    }
    if (!string.IsNullOrWhiteSpace(inputUser.Phone))
    {
        todo.Phone = inputUser.Phone;
    }

    await db.SaveChangesAsync();
    var updatedUser = await db.Users.FindAsync(id);

    return Results.Ok(updatedUser);
})
.RequireAuthorization()
.AddEndpointFilter(EndpointFilterUtility.updateUser);

app.MapGet("/users/{id}/get-distance", async (string id, double latitude, double longitude, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null)
    {
        return Results.NotFound();
    }

    var distance = CalculationUtility.distance(user.Latitude, user.Longitude, latitude, longitude, 'K');

    return Results.Ok(distance);
})
.RequireAuthorization()
.AddEndpointFilter(EndpointFilterUtility.getDistance);

app.Run();

public record LoginModel(string Username, string Password);