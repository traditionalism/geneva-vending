using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using static CitizenFX.Core.Native.API;

namespace geneva_vending.Client
{
    public class ClientMain : BaseScript
    {
        private bool _usingVendingMachine;
        private Prop _canProp;
        private readonly Dictionary<int, int> _vendingMachineModels = new()
        {
            { GetHashKey("prop_vend_soda_01"), GetHashKey("prop_ecola_can") },
            { GetHashKey("prop_vend_soda_02"), GetHashKey("prop_ld_can_01b") }
        };

        private async void LoadAnimDict(string dict)
        {
            RequestAnimDict(dict);
            while (!HasAnimDictLoaded(dict))
            {
                await Delay(0);
            }
        }

        private async void LoadModel(uint model)
        {
            RequestModel(model);
            while (!HasModelLoaded(model))
            {
                await Delay(0);
            }
        }

        private async void LoadAmbientAudioBank(string bank)
        {
            while (!RequestAmbientAudioBank(bank, false))
            {
                await Delay(0);
            }
        }

        private static bool CanUseVendingMachine
        {
            get
            {
                Ped plyPed = Game.PlayerPed;
                return plyPed.IsAlive && !plyPed.IsInVehicle() && !plyPed.IsGettingIntoAVehicle &&
                    !plyPed.IsClimbing && !plyPed.IsVaulting && plyPed.IsOnFoot && !plyPed.IsRagdoll &&
                    !plyPed.IsSwimming;
            }
        }

        private async void BuySoda(Prop vendingMachine)
        {
            _usingVendingMachine = true;
            vendingMachine.State.Set("beingUsed", true, true);
            Ped plyPed = Game.PlayerPed;
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
                LoadModel((uint)canModel);
            }
            else
            {
                _usingVendingMachine = false;
                plyPed.SetConfigFlag(48, false);
                plyPed.IsInvincible = false;
                plyPed.CanBeTargetted = true;
                plyPed.CanRagdoll = true;
                return;
            }

            LoadAmbientAudioBank("VENDING_MACHINE");
            LoadAnimDict("MINI@SPRUNK");

            plyPed.Task.LookAt(vendingMachine, 2000);
            TaskGoStraightToCoord(plyPed.Handle, offset.X, offset.Y, offset.Z, 1.0f, 20000, vendingMachine.Heading, 0.1f);

            while (GetScriptTaskStatus(plyPed.Handle, (uint)GetHashKey("SCRIPT_TASK_GO_STRAIGHT_TO_COORD")) != 7) await Delay(0);

            await plyPed.Task.PlayAnimation("MINI@SPRUNK", "PLYR_BUY_DRINK_PT1", 2f, -4f, -1, AnimationFlags.None, 0f);

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT1") < 0.31f) await Delay(0);

            _canProp = await World.CreatePropNoOffset(canModel, offset, new Vector3(0f, 0f, 0f), true);
            _canProp.IsInvincible = true;
            _canProp.AttachTo(plyPed.Bones[Bone.PH_R_Hand], new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f));

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT1") < 0.98f) await Delay(0);

            await plyPed.Task.PlayAnimation("MINI@SPRUNK", "PLYR_BUY_DRINK_PT2", 4f, -1000f, -1, AnimationFlags.None, 0f);
            ForcePedAiAndAnimationUpdate(plyPed.Handle, false, false);

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT2") < 0.98f) await Delay(0);

            await plyPed.Task.PlayAnimation("MINI@SPRUNK", "PLYR_BUY_DRINK_PT3", 1000f, -4f, -1, AnimationFlags.UpperBodyOnly | AnimationFlags.AllowRotation, 0f);
            ForcePedAiAndAnimationUpdate(plyPed.Handle, false, false);

            while (GetEntityAnimCurrentTime(plyPed.Handle, "MINI@SPRUNK", "PLYR_BUY_DRINK_PT3") < 0.306f) await Delay(0);

            _canProp.Detach();
            _canProp.ApplyForce(new Vector3(6f, 10f, 2f), new Vector3(0f, 0f, 0f), ForceType.MaxForceRot);
            _canProp.MarkAsNoLongerNeeded();

            RemoveAnimDict("MINI@SPRUNK");
            ReleaseAmbientAudioBank();
            SetModelAsNoLongerNeeded((uint)canModel);
            plyPed.SetConfigFlag(48, false);
            plyPed.IsInvincible = false;
            plyPed.CanBeTargetted = true;
            plyPed.CanRagdoll = true;
            _canProp = null;
            _usingVendingMachine = false;
            vendingMachine.State.Set("beingUsed", false, true);
            int sodaLeft = vendingMachine.State.Get("sodaLeft");
            vendingMachine.State.Set("sodaLeft", sodaLeft -= 1, true);
        }

        [Tick]
        private async Task FindVendingMachineTick()
        {
            if (_usingVendingMachine || !CanUseVendingMachine)
            {
                await Delay(3000);
                return;
            }

            Vector3 plyPos = Game.PlayerPed.Position;
            Prop prop = World.GetAllProps()
                .Where(p => _vendingMachineModels.ContainsKey(p.Model))
                .OrderBy(p => Vector3.Distance(p.Position, plyPos))
                .FirstOrDefault();

            if (prop == null)
            {
                await Delay(3000);
                return;
            }

            if (!NetworkGetEntityIsNetworked(prop.Handle)) NetworkRegisterEntityAsNetworked(prop.Handle);

            if (prop.State.Get("sodaLeft") == null || prop.State.Get("beingUsed") == null)
            {
                TriggerServerEvent("geneva-vending:initVendingMachine", prop.NetworkId);
                await Delay(500);
            }

            if (prop.State.Get("beingUsed"))
            {
                await Delay(3500);
                return;
            }

            float dist = Vector3.Distance(plyPos, prop.Position);

            if (dist > 10.0f)
            {
                await Delay(2000);
                return;
            }

            if (dist > 5.0f)
            {
                await Delay(1000);
                return;
            }

            if (prop.State.Get("sodaLeft") > 0 && !IsPauseMenuActive() && dist < 2f)
            {
                Screen.DisplayHelpTextThisFrame("Press ~INPUT_CONTEXT~ to buy a soda for $1.");

                if (Game.IsControlJustReleased(0, Control.Context))
                {
                    BuySoda(prop);
                }
            }
            else if (prop.State.Get("sodaLeft") == 0 && dist < 2f)
            {
                if (!prop.State.Get("markedForReset"))
                {
                    TriggerServerEvent("geneva-vending:markVendingMachineForReset", prop.NetworkId);
                    await Delay(500);
                }

                Screen.DisplayHelpTextThisFrame("Vending machine has run out of sodas.");
            }
        }
    }
}