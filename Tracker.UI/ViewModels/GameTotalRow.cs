using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracker.UI.ViewModels
{
    public sealed record GameTotalRow(string Game, string Active, string Idle);
}
