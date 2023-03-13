using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using static System.DateTime;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using SharpConfig;

namespace geneva_vending.Server
{
    public class ServerMain : BaseScript
    {
        private readonly ConcurrentDictionary<int, DateTime> _resetTimes = new();
        private readonly bool _unlimitedSoda;
        private readonly int _sodaCanCount = 10;
        private readonly int _minutesToReset = 3;

        public ServerMain()
        {
            try
            {
                string data = LoadResourceFile(GetCurrentResourceName(), "config.ini");
                Configuration loaded = Configuration.LoadFromString(data);

                if (!bool.TryParse(loaded["geneva-vending"]["UnlimitedSoda"].StringValue, out _unlimitedSoda))
                {
                    _unlimitedSoda = false;
                }
                if (!int.TryParse(loaded["geneva-vending"]["SodaCanCount"].StringValue, out _sodaCanCount))
                {
                    _sodaCanCount = 10;
                }
                if (!int.TryParse(loaded["geneva-vending"]["MinutesToReset"].StringValue, out _minutesToReset))
                {
                    _minutesToReset = 3;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading 'config.ini': ^3{ex.Message}^0");
            }
        }

        [EventHandler("geneva-vending:initVendingMachine")]
        private void OnInitVendingMachine([FromSource] Player player, int netId)
        {
            Entity vendingMachine = Entity.FromNetworkId(netId);
            if (vendingMachine is null) return;
            Debug.WriteLine($"[^3{Now.ToString("G", CultureInfo.InvariantCulture)}^0] Initializing statebags for vending machine (^3NetID: {netId}, Player: {player.Name} ({player.Handle})^0)");
            vendingMachine.State.Set("beingUsed", false, true);
            if (_unlimitedSoda) return;
            vendingMachine.State.Set("sodaLeft", _sodaCanCount, true);
            vendingMachine.State.Set("markedForReset", false, true);
        }

        [EventHandler("geneva-vending:markVendingMachineForReset")]
        private void OnMarkVendingMachineForReset([FromSource] Player player, int netId)
        {
            Entity vendingMachine = Entity.FromNetworkId(netId);
            if (vendingMachine is null) return;
            Debug.WriteLine($"[^3{Now.ToString("G", CultureInfo.InvariantCulture)}^0] Marking vending machine for reset (^3NetID: {netId}, Player: {player.Name} ({player.Handle})^0)");
            DateTime resetTime = UtcNow.AddMinutes(_minutesToReset);
            _resetTimes[netId] = resetTime;
            vendingMachine.State.Set("markedForReset", true, true);
        }

        [EventHandler("geneva-vending:setAsUsed")]
        private void OnSetAsUsed([FromSource] Player player, int netId)
        {
            Entity vendingMachine = Entity.FromNetworkId(netId);
            if (vendingMachine is null) return;
            Debug.WriteLine($"[^3{Now.ToString("G", CultureInfo.InvariantCulture)}^0] Setting vending machine as used by non-owner {player.Name} ({player.Handle}) (^3NetID: {netId}, Owner: {vendingMachine.Owner.Name} ({vendingMachine.Owner.Handle})^0)");
            vendingMachine.State.Set("beingUsed", true, true);
        }

        [EventHandler("geneva-vending:setAsUnused")]
        private void OnSetAsUnused([FromSource] Player player, int netId, int sodaLeft)
        {
            Entity vendingMachine = Entity.FromNetworkId(netId);
            if (vendingMachine is null) return;
            Debug.WriteLine($"[^3{Now.ToString("G", CultureInfo.InvariantCulture)}^0] Setting vending machine as unused by non-owner {player.Name} ({player.Handle}) (^3NetID: {netId}, Owner: {vendingMachine.Owner.Name} ({vendingMachine.Owner.Handle})^0)");
            vendingMachine.State.Set("beingUsed", false, true);

            if (!_unlimitedSoda)
            {
                vendingMachine.State.Set("sodaLeft", sodaLeft, true);
            }
        }

        [Tick]
        private async Task ResetTick()
        {
            List<int> netIds = new List<int>(_resetTimes.Keys);

            foreach (int netId in netIds)
            {
                if (!_resetTimes.TryGetValue(netId, out DateTime resetTime)) continue;
                if (UtcNow < resetTime) continue;

                Entity vendingMachine = Entity.FromNetworkId(netId);
                if (vendingMachine is null) continue;
                Debug.WriteLine($"[^3{Now.ToString("G", CultureInfo.InvariantCulture)}^0] Resetting vending machine (^3NetID: {netId}^0)");
                vendingMachine.State.Set("sodaLeft", _sodaCanCount, true);
                vendingMachine.State.Set("beingUsed", false, true);
                vendingMachine.State.Set("markedForReset", false, true);

                _resetTimes.TryRemove(netId, out resetTime);
            }

            await Delay(20000);
        }
    }
}