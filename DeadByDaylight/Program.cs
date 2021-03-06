using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics;
using SharpDX.XInput;
using WeScriptWrapper;
using WeScript.SDK.UI;
using WeScript.SDK.UI.Components;

namespace DeadByDaylight
{
    class Program
    {
        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static IntPtr GameBase = IntPtr.Zero;
        public static IntPtr GameSize = IntPtr.Zero;


        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor SurvColor = new MenuColor("srvcolor", "Survivors Color", new SharpDX.Color(0, 255, 0, 60));
                public static readonly MenuBool DrawSurvivorBox = new MenuBool("srvbox", "Draw Survivors Box", true);
                public static readonly MenuColor KillerColor = new MenuColor("kilcolor", "Killers Color", new SharpDX.Color(255, 0, 0, 100));
                public static readonly MenuBool DrawKillerBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                //public static readonly MenuSlider OffsetGuesser = new MenuSlider("ofsgues", "Guess the offset", 10, 1, 250);
            }
        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.SurvColor,
                Components.VisualsComponent.DrawSurvivorBox,
                Components.VisualsComponent.KillerColor,
                Components.VisualsComponent.DrawKillerBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                //Components.VisualsComponent.OffsetGuesser,
            };


            RootMenu = new Menu("dbdexample", "WeScript.app DeadByDaylight Example Assembly", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
            };
            RootMenu.Attach();
        }


        //public static string GetNameFromID(uint ID) //really bad implementation - probably needs fixing, plus it's better to use it as a dumper once at startup and cache ids
        //{
        //    if (processHandle != IntPtr.Zero)
        //    {
        //        if (GameBase != IntPtr.Zero)
        //        {
        //            var GNames = Memory.ReadPointer(processHandle, (IntPtr)GameBase.ToInt64() + 0x58EEED8, isWow64Process);
        //            if (GNames != IntPtr.Zero)
        //            {
        //                for (uint i = 0; i <= 12; i++)
        //                {
        //                    var firstChunk = Memory.ReadPointer(processHandle, (IntPtr)(GNames.ToInt64() + i * 8), isWow64Process);
        //                    if (firstChunk != IntPtr.Zero)
        //                    {
        //                        var fNamePtr = Memory.ReadPointer(processHandle, (IntPtr)(firstChunk.ToInt64() + (ID / 0x4000) * 8), isWow64Process);
        //                        if (fNamePtr != IntPtr.Zero)
        //                        {
        //                            var fName = Memory.ReadPointer(processHandle, (IntPtr)(fNamePtr.ToInt64() + 8 * (ID % 0x4000)), isWow64Process);
        //                            if (fName != IntPtr.Zero)
        //                            {
        //                                var name = Memory.ReadString(processHandle, (IntPtr)fName.ToInt64() + 0xC, false, 64);
        //                                if (name.Length > 0) return name;
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    return "NULL";
        //}

        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app DBD Example Assembly Loaded! (Don't forget EAC Bypass!)");
            InitializeMenu();
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }


        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowName("DeadByDaylight  "); //why the devs added spaces after the name?!
                if (wndHnd != IntPtr.Zero) //if it exists
                {
                    //Console.WriteLine("weheree");
                    var calcPid = Memory.GetPIDFromHWND(wndHnd); //get the PID of that same process
                    if (calcPid > 0) //if we got the PID
                    {
                        processHandle = Memory.OpenProcess(PROCESS_ALL_ACCESS, calcPid); //the driver will get a stripped handle, but doesn't matter, it's still OK
                        if (processHandle != IntPtr.Zero)
                        {
                            //if we got access to the game, check if it's x64 bit, this is needed when reading pointers, since their size is 4 for x86 and 8 for x64
                            isWow64Process = Memory.IsProcess64Bit(processHandle); //we know DBD is 64 bit but anyway...
                        }
                        else
                        {
                            Console.WriteLine("failed to get handle");
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowName("DeadByDaylight  "); //why the devs added spaces after the name?!
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();

                    if (GameBase == IntPtr.Zero) //do we have access to Gamebase address?
                    {
                        GameBase = Memory.GetModule(processHandle, null, isWow64Process); //if not, find it
                    }
                    else
                    {
                        if (GameSize == IntPtr.Zero)
                        {
                            GameSize = Memory.GetModuleSize(processHandle, null, isWow64Process);
                        }
                        else
                        {
                            //Console.WriteLine($"GameBase: {GameBase.ToString("X")}"); //easy way to check if we got reading rights
                            //Console.WriteLine($"GameSize: {GameSize.ToString("X")}"); //easy way to check if we got reading rights
                        }
                    }

                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;
                    //clear your offsets, modules
                    GameBase = IntPtr.Zero;
                    GameSize = IntPtr.Zero;
                }
            }
        }

        private static void OnRenderer(int fps, EventArgs args)
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off

            //Renderer.DrawText($"min: {(151000 + (Components.VisualsComponent.OffsetGuesser.Value * 10)).ToString()} max: {(151100 + (Components.VisualsComponent.OffsetGuesser.Value * 10)).ToString()}", new Vector2(300, 300));
            Matrix viewProj = new Matrix();
            var UWorld = Memory.ReadPointer(processHandle, (IntPtr)GameBase.ToInt64() + 0x5A29158, isWow64Process); //48 8B 1D ?? ?? ?? ?? 48 85 DB 74 3B 41 || mov rbx,[DeadByDaylight-Win64-Shipping.exe+5A29158]
            if (UWorld != IntPtr.Zero)
            {
                var UGameInstance = Memory.ReadPointer(processHandle, (IntPtr)UWorld.ToInt64() + 0x170, isWow64Process);
                if (UGameInstance != IntPtr.Zero)
                {
                    var localPlayerArray = Memory.ReadPointer(processHandle, (IntPtr)UGameInstance.ToInt64() + 0x40, isWow64Process);
                    if (localPlayerArray != IntPtr.Zero)
                    {
                        var ULocalPlayer = Memory.ReadPointer(processHandle, localPlayerArray, isWow64Process);
                        if (ULocalPlayer != IntPtr.Zero)
                        {
                            var CameraPtr = Memory.ReadPointer(processHandle, (IntPtr)ULocalPlayer.ToInt64() + 0xB8, isWow64Process);
                            if (CameraPtr != IntPtr.Zero)
                            {
                                viewProj = Memory.ReadMatrix(processHandle, (IntPtr)(CameraPtr.ToInt64() + 0x1FC));
                            }
                        }
                    }
                }
                var ULevel = Memory.ReadPointer(processHandle, (IntPtr)UWorld.ToInt64() + 0x38, isWow64Process);
                if (ULevel != IntPtr.Zero)
                {
                    var AActors = Memory.ReadPointer(processHandle, (IntPtr)ULevel.ToInt64() + 0xA0, isWow64Process);
                    var ActorCnt = Memory.ReadUInt32(processHandle, (IntPtr)ULevel.ToInt64() + 0xA8);
                    if ((AActors != IntPtr.Zero) && (ActorCnt > 0))
                    {
                        for (uint i = 0; i <= ActorCnt; i++)
                        {
                            var AActor = Memory.ReadPointer(processHandle, (IntPtr)(AActors.ToInt64() + i * 8), isWow64Process);
                            if (AActor != IntPtr.Zero)
                            {
                                var USceneComponent = Memory.ReadPointer(processHandle, (IntPtr)AActor.ToInt64() + 0x168, isWow64Process);
                                if (USceneComponent != IntPtr.Zero)
                                {
                                    var tempVec = Memory.ReadVector3(processHandle, (IntPtr)USceneComponent.ToInt64() + 0x160);
                                    var AActorID = Memory.ReadUInt32(processHandle, (IntPtr)AActor.ToInt64() + 0x18);
                                    //if ((AActorID > 0) && (AActorID < 200000))
                                    //{
                                    //    the check below is a ghetto way to "guess" the ID of players and killers using a slider in the menu
                                    //    Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                    //    if ((AActorID >= 151000 + (Components.VisualsComponent.OffsetGuesser.Value * 10)) && (AActorID <= 151100 + (Components.VisualsComponent.OffsetGuesser.Value * 10)))
                                    //    {
                                    //        if (Renderer.WorldToScreen(tempVec, out vScreen_d3d11, viewProj, wndMargins, wndSize, W2SType.TypeD3D11))
                                    //        {
                                    //            Renderer.DrawText($"ID: {AActorID.ToString()} Name: {GetNameFromID(AActorID)}", vScreen_d3d11, new Color(255, 255, 255), 12, TextAlignment.centered, false);
                                    //        }
                                    //    }
                                    //}

                                    if (Components.VisualsComponent.DrawTheVisuals.Enabled) //this should have been placed earlier?
                                    {
                                        if (AActorID == 152012) //survivors on 3.7.2
                                        {
                                            Vector2 vScreen_h3ad = new Vector2(0, 0);
                                            Vector2 vScreen_f33t = new Vector2(0, 0);
                                            if (Renderer.WorldToScreen(new Vector3(tempVec.X, tempVec.Y, tempVec.Z + 60.0f), out vScreen_h3ad, viewProj, wndMargins, wndSize, W2SType.TypeD3D11))
                                            {
                                                Renderer.WorldToScreen(new Vector3(tempVec.X, tempVec.Y, tempVec.Z - 130.0f), out vScreen_f33t, viewProj, wndMargins, wndSize, W2SType.TypeD3D11);
                                                if (Components.VisualsComponent.DrawSurvivorBox.Enabled)
                                                {
                                                    Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Components.VisualsComponent.SurvColor.Color, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                }
                                            }
                                        }
                                        if (AActorID == 152720) //killers on 3.7.2
                                        {
                                            Vector2 vScreen_h3ad = new Vector2(0, 0);
                                            Vector2 vScreen_f33t = new Vector2(0, 0);
                                            if (Renderer.WorldToScreen(new Vector3(tempVec.X, tempVec.Y, tempVec.Z + 80.0f), out vScreen_h3ad, viewProj, wndMargins, wndSize, W2SType.TypeD3D11))
                                            {
                                                Renderer.WorldToScreen(new Vector3(tempVec.X, tempVec.Y, tempVec.Z - 150.0f), out vScreen_f33t, viewProj, wndMargins, wndSize, W2SType.TypeD3D11);
                                                if (Components.VisualsComponent.DrawKillerBox.Enabled)
                                                {
                                                    Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Components.VisualsComponent.KillerColor.Color, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }
    }
}
