using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecare.Application.Commands.NewCard.NewClientEquipment
{
    public sealed record SaveClientEquipementRequest(
     string? ClientName,
     string? CarteSLV,
     string? Matricule,
     string? ChauffeurName,
     string? RfidHex,
     string? CodeClientSAP,
     string? Type,
     int? PlombsNumber,
     int? PTAC,
     int? TARE,
     string? Status,
     string? CodeTransporteurSap,
     string? TransporteurName,
     string? CodeTruckSap,
     string? CodeTransporteurSapCimar,
     string? PermisConducteur,
     int? IsClient,
     int? IsTransporteur,
     int? IsDriver,
     string? TruckType
 );

    public sealed record SaveClientEquipementResult(int ClientEquipementId);

    public sealed record SaveClientEquipementCommand(SaveClientEquipementRequest Data)
        : IRequest<SaveClientEquipementResult>;
}
