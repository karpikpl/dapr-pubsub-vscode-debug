using System.Text.Json.Serialization;
using Dapr;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseDeveloperExceptionPage(); }

// Dapr will send serialized event object vs. being raw CloudEvent
app.UseCloudEvents();

// needed for Dapr pub/sub routing
app.MapSubscribeHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

const string DAPR_STORE_NAME = "orderstore-redis";
var client = new Dapr.Client.DaprClientBuilder().Build();

// Dapr subscription in [Topic] routes orders topic to this route
app.MapPost("/orders", [Topic("order-pubsub-redis", "orders")] async (Order order) =>
{
    Console.WriteLine("Subscriber received : " + order);

    // Save state into the state store
    await client.SaveStateAsync(DAPR_STORE_NAME, order.OrderId.ToString(), order);

    return Results.Ok(order);
});

app.MapGet("/orders/{id}", async (int id) =>
{
    // Get state from the state store
    var state = await client.GetStateAsync<Order>(DAPR_STORE_NAME, id.ToString());
    if (state != null)
    {
        return Results.Ok(state);
    }
    else
    {
        return Results.NotFound();
    }
})
.WithName("GetOrder")
.WithOpenApi();

app.MapGet("/tmp", async () =>
{
    // Get state from the state store
    const int key = 1234;
    await client.SaveStateAsync<Order>(DAPR_STORE_NAME, key.ToString(), new Order(key));

    var state = await client.GetStateAsync<Order>(DAPR_STORE_NAME, key.ToString());
    if (state != null)
    {
        return Results.Ok(state);
    }
    else
    {
        return Results.NotFound();
    }
})
.WithName("Test State")
.WithOpenApi();

await app.RunAsync();

public record Order([property: JsonPropertyName("orderId")] int OrderId);
