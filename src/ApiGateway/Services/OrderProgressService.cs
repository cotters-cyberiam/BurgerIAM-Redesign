using System.Net;
using System.Threading.Channels;
using Grpc.Net.Client;
using ProtoOrder = BurgerIAM.Protos.Order;
using ProtoKitchen = BurgerIAM.Protos.Kitchen;
using ProtoDelivery = BurgerIAM.Protos.Delivery;

namespace ApiGateway.Services;

public sealed class OrderProgressService : BackgroundService
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly string _orderUrl;
    private readonly string _kitchenUrl;
    private readonly string _deliveryUrl;

    public OrderProgressService(IConfiguration config)
    {
        var services = config.GetSection("Services");
        _orderUrl = services["Order"] ?? "http://localhost:5063";
        _kitchenUrl = services["Kitchen"] ?? "http://localhost:5085";
        _deliveryUrl = services["Delivery"] ?? "http://localhost:5096";
    }

    public async Task EnqueueOrderAsync(string orderId, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(orderId, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var orderId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessOrderAsync(orderId, stoppingToken);
            }
            catch
            {
                // Log and continue
            }
        }
    }

    private async Task ProcessOrderAsync(string orderId, CancellationToken ct)
    {
        using var kitchenChannel = GrpcChannel.ForAddress(_kitchenUrl);
        var kitchenClient = new ProtoKitchen.KitchenService.KitchenServiceClient(kitchenChannel);

        using var deliveryChannel = GrpcChannel.ForAddress(_deliveryUrl);
        var deliveryClient = new ProtoDelivery.DeliveryService.DeliveryServiceClient(deliveryChannel);

        using var http = new HttpClient();

        HttpRequestMessage StatusReq(int s) => new(HttpMethod.Post, $"{_orderUrl}/api/internal/orders/{orderId}/status?status={s}")
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        await kitchenClient.StartPreparingAsync(new ProtoKitchen.StartPreparingRequest { OrderId = orderId, Station = "Grill" }, cancellationToken: ct);
        await http.SendAsync(StatusReq(3), ct);

        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        await kitchenClient.MarkAsReadyAsync(new ProtoKitchen.MarkAsReadyRequest { OrderId = orderId }, cancellationToken: ct);
        await http.SendAsync(StatusReq(4), ct);

        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        var delivery = await deliveryClient.AssignDeliveryAsync(new ProtoDelivery.AssignDeliveryRequest { OrderId = orderId, DeliveryAddress = "" }, cancellationToken: ct);
        await http.SendAsync(StatusReq(5), ct);

        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        await deliveryClient.UpdateDeliveryStatusAsync(new ProtoDelivery.UpdateDeliveryStatusRequest { DeliveryId = delivery.Id, Status = 4 }, cancellationToken: ct);
        await http.SendAsync(StatusReq(6), ct);
    }
}
