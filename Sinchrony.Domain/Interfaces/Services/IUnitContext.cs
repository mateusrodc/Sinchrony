using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Domain.Interfaces.Services
{
    public interface IUnitContext
    {
        Guid? UnitId { get; }
        bool IsGlobalAdmin { get; }
        Guid? UserId { get; }
    }
}
