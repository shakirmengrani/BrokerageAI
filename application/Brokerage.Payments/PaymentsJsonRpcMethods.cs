using Brokerage.Common;

namespace Brokerage.Payments;

public sealed class PaymentsJsonRpcMethods : IJsonRpcMethodProvider
{
    public IEnumerable<JsonRpcMethod> GetMethods()
    {
        yield return new("payments.create", async call =>
        {
            var accountId = call.Required<Guid>("accountId");
            call.RequireAccountAccess(accountId);
            return await call.Service<PaymentsService>().CreatePaymentAsync(
                accountId, call.Required<string>("paymentReference"), call.Required<string>("idempotencyKey"),
                call.Required<PaymentDirection>("direction"), call.Required<decimal>("amount"),
                call.Required<string>("currency"), call.CancellationToken);
        });
        yield return new("payments.get", async call =>
        {
            var payment = await call.Service<PaymentsService>().GetPaymentAsync(
                call.Required<Guid>("paymentId"), call.CancellationToken);
            if (payment is not null)
                call.RequireAccountAccess(payment.AccountId);
            return payment;
        });
    }
}
