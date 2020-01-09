using System;
using System.Collections.Generic;
using System.Text;

namespace LunarLib.Services
{
    public interface ISystemService : IDisposable
    {

        void OnUpdateStatus(EventArgs e);

        void AddMods(IEnumerable<string> filenames);

        event EventHandler UpdateStatus;
    }

    public class UpdateStatusEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
