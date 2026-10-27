using Brokerage.Common;

namespace Brokerage.Orders;

public sealed class OrdersJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("orders.place", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<OrdersService>().PlaceOrderAsync(
                accountId, call.Required<Guid>("instrumentId"), call.Required<string>("clientOrderReference"),
                call.Required<OrderSide>("side"), call.Required<OrderType>("type"),
                call.Required<TimeInForce>("timeInForce"), call.Required<decimal>("quantity"),
                call.Optional<decimal>("limitPrice"), call.CancellationToken);
        });
        yield return new("orders.cancel", async call =>
        {
            var service = call.Service<OrdersService>();
            var orderId = call.Required<Guid>("orderId");
            call.RequireAccountAccess(await service.GetOrderAccountIdAsync(orderId, call.CancellationToken));
            await service.CancelOrderAsync(orderId, call.CancellationToken);
            return null;
        }, RequiredRoles: ["BrokerageTrader", "BrokerageOperations", "Administrator"]);
        yield return new("orders.accept", async call =>
        {
            await call.Service<OrdersService>().AcceptOrderAsync(
                call.Required<Guid>("orderId"), call.CancellationToken);
            return null;
        }, RequiredRoles: ["Administrator"]);
    }
}
