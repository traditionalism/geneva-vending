using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace geneva_vending.Server
{
    public class ServerMain : BaseScript
    {
        private readonly ConcurrentDictionary<int, System.DateTime> _resetTimes = new();

        [EventHandler("geneva-vending:initVendingMachine")]
        private void OnInitVendingMachine([FromSource] Player player, int netId)
        {
            Entity vendingMachine = Entity.FromHandle(NetworkGetEntityFromNetworkId(netId));
            if (vendingMachine != null)
            {
                Debug.WriteLine("Initializing statebag(s) for ^3NetID: {0}^0.", netId);
                vendingMachine.State.Set("sodaLeft", 3, true);
                vendingMachine.State.Set("beingUsed", false, true);
                vendingMachine.State.Set("markedForReset", false, true);
            }
        }

        [EventHandler("geneva-vending:markVendingMachineForReset")]
        private void OnMarkVendingMachineForReset([FromSource] Player player, int netId)
        {
            Entity vendingMachine = Entity.FromHandle(NetworkGetEntityFromNetworkId(netId));
            if (vendingMachine != null)
            {
                Debug.WriteLine("Marking vending machine for reset, ^3NetID: {0}^0", netId);
                System.DateTime resetTime = System.DateTime.UtcNow.AddMinutes(3);
                _resetTimes[netId] = resetTime;
                vendingMachine.State.Set("markedForReset", true, true);
            }
        }

        [Tick]
        private async Task ResetTick()
        {
            List<int> netIds = new List<int>(_resetTimes.Keys);

            foreach (int netId in netIds)
            {
                if (!_resetTimes.TryGetValue(netId, out var resetTime)) continue;
                if (System.DateTime.UtcNow < resetTime) continue;

                Entity vendingMachine = Entity.FromHandle(NetworkGetEntityFromNetworkId(netId));
                if (vendingMachine != null)
                {
                    Debug.WriteLine("Resetting vending machine, ^3NetID: {0}^0", netId);
                    vendingMachine.State.Set("sodaLeft", 3, true);
                    vendingMachine.State.Set("beingUsed", false, true);
                    vendingMachine.State.Set("markedForReset", false, true);
                }

                _resetTimes.TryRemove(netId, out resetTime);
            }

            await Delay(180000);
        }
    }
}