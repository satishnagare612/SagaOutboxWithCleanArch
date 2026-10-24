using SagaOutBoxCleanArch.Domain.Orders;

namespace SagaOutBoxCleanArch.Application.Orders;

public sealed record OrderDto(
    Guid Id,
    Guid SagaId,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    string Status,
    DateTime CreatedAt)
{
    public static OrderDto From(Order order) => new(
        order.Id, order.SagaId, order.ProductId, order.Quantity, order.UnitPrice, order.Status, order.CreatedAt);
}

public sealed record CreateOrderInput(string ProductId, int Quantity, decimal UnitPrice);
