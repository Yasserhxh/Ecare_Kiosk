using MediatR;
using Ecare.Shared;
using System.ComponentModel.DataAnnotations;

namespace Ecare.Application.Commands;
public sealed record ConfirmOrderCommand([property:Required] string OrderNumber) : IRequest<Result>;
