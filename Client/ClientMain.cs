using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using static CitizenFX.Core.Native.API;
using SharpConfig;

namespace geneva_vending.Client
{
    public class ClientMain : BaseScript
    {
        private readonly bool _unlimitedSoda;
        private readonly Dictionary<int, int> _vendingMachineModels = new()
        {
            { GetHashKey("prop_vend_soda_01"), GetHashKey("prop_ecola_can") },
            { GetHashKey("prop_vend_soda_02"), GetHashKey("prop_ld_can_01b") }
        };
        private static bool CanUseVendingMachine =>
            Game.PlayerPed.IsAlive &&
            !Game.PlayerPed.IsInVehicle() &&
            !Game.PlayerPed.IsGettingIntoAVehicle &&
            !Game.PlayerPed.IsClimbing &&
            !Game.PlayerPed.IsVaulting &&
            Game.PlayerPed.IsOnFoot &&
            !Game.PlayerPed.IsRagdoll &&
            !Game.PlayerPed.IsSwimming;

        public ClientMain()
        {
            try
            {
                string data = LoadResourceFile(GetCurrentResourceName(), "config.ini");
                Configuration loaded = Configuration.LoadFromString(data);

                if (!System.Boolean.TryParse(loaded["geneva-vending"]["UnlimitedSoda"].StringValue, out _unlimitedSoda))
                {
                    _unlimitedSoda = false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Error loading 'config.ini': {ex.Message}");
            }
        }

        private async Task LoadAnimDict(string dict)
        {
            RequestAnimDict(dict);
            while (!HasAnimDictLoaded(dict))
            {
                await Delay(0);
            }
        }

        private async Task LoadModel(uint model)
        {
            RequestModel(model);
            while (!HasModelLoaded(model))
            {
                await Delay(0);
            }
        }

        private async Task LoadAmbientAudioBank(string bank)
        {
            while (!RequestAmbientAudioBank(bank, false))
            {
                await Delay(0);
            }
        }

        private async Task BuySoda(Prop vendingMachine)
        {
            ClearHelp(true);
            Ped plyPed = Game.PlayerPed;
            plyPed.Task.ClearAll();

            bool owner = NetworkGetEntityOwner(vendingMachine.Handle) == Game.Player.Handle;

            if (!owner)
            {
                TriggerServerEvent("geneva-vending:setAsUsed", vendingMachine.NetworkId);
            }
            else
            {
                vendingMachine.State.Set("beingUsed", true, true);
            }

            Vector3 offset = vendingMachine.GetOffsetPosition(new Vector3(0f, -0.97f, 0.05f));

            plyPed.SetConfigFlag(48, true);
            SetPedCurrentWeaponVisible(plyPed.Handle, false, true, true, false);
            SetPedStealthMovement(plyPed.Handle, false, "DEFAULT_ACTION");
            SetPedResetFlag(plyPed.Handle, 322, true);
            plyPed.IsInvincible = true;
            plyPed.CanBeTargetted = false;
            plyPed.CanRagdoll = false;

            if (_vendingMachineModels.TryGetValue(vendingMachine.Model.Hash, out int canModel))
            {
                await LoadModel((uint)canModel);
            }
            else
            {
                plyPed.SetConfigFlag(48, false);
                plyPed.IsInvincible = false;
                plyPed.CanBeTargetted = true;
                plyPed.CanRagdoll = true;
                return;
            }

            await LoadAmbientAudioBank("VENDING_MACHINE");
            await LoadAnimDict("MINI@SPRUNK");

            plyPed.Task.LookAt(vendingMachine, 2000);
            TaskGoStraightToCoord(plyPed.Handle, offset.X, offset.Y, offset.Z, 1.0f, 20000, vendingMachine.Heading, 0.1f);

            while (GetScriptTaskStatus(plyPed.Handle, (uint)GetHashKey("SCRIPT_TASK_GO_STRAIGHT_TO_COORD")) != 7) await Delay(0);

            await plyPed.Task.PlayAnimation("MINI@SPRUNK", "PLYR_BUY_DRINK_PT1", 2f, -4f, -1, AnimationFlags.None, 0f);

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT1") < 0.31f) await Delay(0);

            Prop canProp = await World.CreatePropNoOffset(canModel, offset, new Vector3(0f, 0f, 0f), true);
            canProp.IsInvincible = true;
            canProp.AttachTo(plyPed.Bones[Bone.PH_R_Hand], new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT1") < 0.98f) await Delay(0);

            await plyPed.Task.PlayAnimation("MINI@SPRUNK", "PLYR_BUY_DRINK_PT2", 4f, -1000f, -1, AnimationFlags.None, 0f);
            ForcePedAiAndAnimationUpdate(plyPed.Handle, false, false);

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT2") < 0.98f) await Delay(0);

            await plyPed.Task.PlayAnimation("MINI@SPRUNK", "PLYR_BUY_DRINK_PT3", 1000f, -4f, -1, AnimationFlags.UpperBodyOnly | AnimationFlags.AllowRotation, 0f);
            ForcePedAiAndAnimationUpdate(plyPed.Handle, false, false);

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT3") < 0.306f) await Delay(0);

            canProp.Detach();
            canProp.ApplyForce(new Vector3(6f, 10f, 2f), new Vector3(0f, 0f, 0f), ForceType.MaxForceRot);
            canProp.MarkAsNoLongerNeeded();

            RemoveAnimDict("MINI@SPRUNK");
            ReleaseAmbientAudioBank();
            SetModelAsNoLongerNeeded((uint)canModel);
            plyPed.SetConfigFlag(48, false);
            plyPed.Task.ClearAll();
            plyPed.IsInvincible = false;
            plyPed.CanBeTargetted = true;
            plyPed.CanRagdoll = true;
            if (!owner)
            {
                if (!_unlimitedSoda)
                {
                    int sodaLeft = vendingMachine.State.Get("sodaLeft");
                    TriggerServerEvent("geneva-vending:setAsUnused", vendingMachine.NetworkId, sodaLeft -= 1);
                }
                else
                {
                    TriggerServerEvent("geneva-vending:setAsUnused", vendingMachine.NetworkId);
                }
            }
            else
            {
                vendingMachine.State.Set("beingUsed", false, true);
                if (!_unlimitedSoda)
                {
                    int sodaLeft = vendingMachine.State.Get("sodaLeft");
                    vendingMachine.State.Set("sodaLeft", sodaLeft -= 1, true);
                }
            }
        }

        [Tick]
        private async Task FindVendingMachineTick()
        {
            if (!CanUseVendingMachine)
            {
                await Delay(2500);
                return;
            }

            Vector3 plyPos = Game.PlayerPed.Position;
            Prop prop = World.GetAllProps()
                .Where(p => _vendingMachineModels.ContainsKey(p.Model))
                .OrderBy(p => Vector3.DistanceSquared(p.Position, plyPos))
                .FirstOrDefault();

            if (prop == null)
            {
                await Delay(4000);
                return;
            }

            if (!NetworkGetEntityIsNetworked(prop.Handle)) NetworkRegisterEntityAsNetworked(prop.Handle);

            if (prop.State.Get("beingUsed") == null)
            {
                TriggerServerEvent("geneva-vending:initVendingMachine", prop.NetworkId);
                await Delay(1000);
            }

            if (prop.State.Get("beingUsed") == null || prop.State.Get("beingUsed"))
            {
                await Delay(3000);
                return;
            }

            float dist = Vector3.DistanceSquared(plyPos, prop.Position);

            if (dist > 5.0f)
            {
                await Delay(1500);
                return;
            }

            if ((!_unlimitedSoda && prop.State.Get("sodaLeft") > 0) || _unlimitedSoda)
            {
                if (!IsPauseMenuActive() && dist < 1.5f)
                {
                    Screen.DisplayHelpTextThisFrame("Press ~INPUT_CONTEXT~ to buy a soda for $1.");

                    if (Game.IsControlJustReleased(0, Control.Context))
                    {
                        await BuySoda(prop);
                    }
                }
            }
            else
            {
                if (!_unlimitedSoda && prop.State.Get("markedForReset") == null || !prop.State.Get("markedForReset"))
                {
                    TriggerServerEvent("geneva-vending:markVendingMachineForReset", prop.NetworkId);
                    await Delay(500);
                }

                Screen.DisplayHelpTextThisFrame("Vending machine has run out of sodas.");
            }
        }
    }
}