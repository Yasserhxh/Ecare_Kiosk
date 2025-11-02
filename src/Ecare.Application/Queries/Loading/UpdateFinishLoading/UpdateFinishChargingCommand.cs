using Ecare.Shared;
using MediatR;

public sealed record UpdateFinishChargingCommand(string Matricule,string BonDeCommande) : IRequest<Result<int>>;


