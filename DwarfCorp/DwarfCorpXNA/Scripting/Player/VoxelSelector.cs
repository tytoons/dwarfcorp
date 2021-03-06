using System;
using System.Collections.Generic;
using System.Linq;
using DwarfCorp.GameStates;
using LibNoise;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Math = System.Math;

namespace DwarfCorp
{
    /// <summary>
    /// The behavior of the voxel selector depends on its type.
    /// </summary>
    public enum VoxelSelectionType
    {
        /// <summary>
        /// Selects only filled voxels
        /// </summary>
        SelectFilled,
        /// <summary>
        /// Selects only empty voxels
        /// </summary>
        SelectEmpty
    }

    /// <summary>
    /// The voxel selector can be configured to select using a 
    /// parametric brush.
    /// </summary>
    public enum VoxelBrush
    {
        /// <summary>
        /// Default selection type. Selects everything in a bounding box.
        /// </summary>
        Box,
        /// <summary>
        /// Selects voxels in a shell on the outside of a box.
        /// </summary>
        Shell,
        /// <summary>
        /// Selects voxels in a stairstep pattern along the longest
        /// axis.
        /// </summary>
        Stairs
    }

    /// <summary>
    /// This class handles selecting and deselecting regions of voxels with the mouse. It is used
    /// in multiple tools.
    /// </summary>
    public class VoxelSelector
    {

        /// <summary>
        /// Called whenever the mouse cursor is dragged.
        /// </summary>
        /// <param name="voxels">The voxels selected.</param>
        /// <param name="button">The button depressed.</param>
        public delegate void OnDragged(List<TemporaryVoxelHandle> voxels, InputManager.MouseButton button);

        /// <summary>
        /// Called whenever the left mouse button is pressed
        /// </summary>
        /// <returns>The voxel under the mouse</returns>
        public delegate TemporaryVoxelHandle OnLeftPressed();

        /// <summary>
        /// Called whenever the left mouse button is released.
        /// </summary>
        /// <returns>A list of voxels that were selected</returns>
        public delegate List<TemporaryVoxelHandle> OnLeftReleased();

        /// <summary>
        /// Called whenever the right mouse button is pressed.
        /// </summary>
        /// <returns>The voxel under the mouse</returns>
        public delegate TemporaryVoxelHandle OnRightPressed();

        /// <summary>
        /// Called whenever the right mouse button is released
        /// </summary>
        /// <returns>List of voxels selected.</returns>
        public delegate List<TemporaryVoxelHandle> OnRightReleased();

        /// <summary>
        /// Called whenever a list of voxels have been selected.
        /// </summary>
        /// <param name="voxels">The voxels.</param>
        /// <param name="button">The button depressed to select the voxels.</param>
        public delegate void OnSelected(List<TemporaryVoxelHandle> voxels, InputManager.MouseButton button);

        /// <summary>
        /// The first voxel selected before the player begins dragging the mouse.
        /// </summary>
        public TemporaryVoxelHandle FirstVoxel = TemporaryVoxelHandle.InvalidHandle;
        /// <summary>
        /// The voxel currently under the mouse.
        /// </summary>
        public TemporaryVoxelHandle VoxelUnderMouse = TemporaryVoxelHandle.InvalidHandle;
        /// <summary>
        /// True if the left mouse button is depressed.
        /// </summary>
        private bool isLeftPressed;
        /// <summary>
        /// True if the right mouse button is depressed.
        /// </summary>
        private bool isRightPressed;


        /// <summary>
        /// The brush to use for selection.
        /// </summary>
        public VoxelBrush Brush = VoxelBrush.Box;

        public SoundSource ClickSound;
        public SoundSource DragSound;
        public SoundSource ReleaseSound;

        public WorldManager World;

        public VoxelSelector(WorldManager world, Camera camera, GraphicsDevice graphics, ChunkManager chunks)
        {
            World = world;
            SelectionType = VoxelSelectionType.SelectEmpty;
            SelectionColor = Color.White;
            SelectionWidth = 0.1f;
            CurrentWidth = 0.08f;
            CurrentColor = Color.White;
            CameraController = camera;
            Graphics = graphics;
            Chunks = chunks;
            SelectionBuffer = new List<TemporaryVoxelHandle>();
            LeftPressed = LeftPressedCallback;
            RightPressed = RightPressedCallback;
            LeftReleased = LeftReleasedCallback;
            RightReleased = RightReleasedCallback;
            Dragged = DraggedCallback;
            Selected = SelectedCallback;
            Enabled = true;
            DeleteColor = Color.Red;
            BoxYOffset = 0;
            LastMouseWheel = 0;
            ClickSound = SoundSource.Create(ContentPaths.Audio.Oscar.sfx_gui_change_selection);
            ClickSound.RandomPitch = false;
            DragSound = SoundSource.Create(ContentPaths.Audio.Oscar.sfx_gui_click_voxel);
            DragSound.RandomPitch = false;
            ReleaseSound = SoundSource.Create(ContentPaths.Audio.Oscar.sfx_gui_confirm_selection);
            ReleaseSound.RandomPitch = false;
        }

        /// <summary>
        /// The color to draw while left mouse button is clicked.
        /// </summary>
        /// <value>
        /// The color of the selection.
        /// </value>
        public Color SelectionColor { get; set; }
        /// <summary>
        /// The color to draw while right mouse button is clicked
        /// </summary>
        /// <value>
        /// The color of the delete.
        /// </value>
        public Color DeleteColor { get; set; }
        /// <summary>
        /// The current color to draw the selection box.
        /// </summary>
        /// <value>
        /// The color of the current.
        /// </value>
        public Color CurrentColor { get; set; }
        /// <summary>
        /// The width of lines to draw while selecting.
        /// </summary>
        /// <value>
        /// The width of the current.
        /// </value>
        public float CurrentWidth { get; set; }
        /// <summary>
        /// The width of the lines to draw while selecting.
        /// </summary>
        /// <value>
        /// The width of the selection.
        /// </value>
        public float SelectionWidth { get; set; }
        /// <summary>
        /// Gets or sets the type of the selection.
        /// </summary>
        /// <value>
        /// The type of the selection.
        /// </value>
        public VoxelSelectionType SelectionType { get; set; }
        /// <summary>
        /// Called when voxels are selected.
        /// </summary>
        public OnSelected Selected { get; set; }
        public Camera CameraController { get; set; }
        public GraphicsDevice Graphics { get; set; }
        public ChunkManager Chunks { get; set; }
        /// <summary>
        /// This is the list of voxels currently selected.
        /// </summary>
        /// <value>
        /// The selection buffer.
        /// </value>
        public List<TemporaryVoxelHandle> SelectionBuffer { get; set; }

        /// <summary>
        /// If this selector is enabled, when the player clicks they 
        /// will be able to select voxels.
        /// </summary>
        /// <value>
        ///   <c>true</c> if enabled; otherwise, <c>false</c>.
        /// </value>
        public bool Enabled { get; set; }
        /// <summary>
        /// This value indicates how many voxels above or below the mouse
        /// the player is currently selecting (for example to dig pits using ALT)
        /// </summary>
        /// <value>
        /// The box y offset.
        /// </value>
        public float BoxYOffset { get; set; }

        private int PrevBoxYOffsetInt = 0;
        /// <summary>
        /// Gets or sets the last value of the mouse wheel.
        /// </summary>
        /// <value>
        /// The last mouse wheel.
        /// </value>
        public int LastMouseWheel { get; set; }
        public event OnLeftPressed LeftPressed;
        public event OnRightPressed RightPressed;
        public event OnLeftReleased LeftReleased;
        public event OnRightReleased RightReleased;
        public event OnDragged Dragged;

        public void Update()
        {
            MouseState mouse = Mouse.GetState();
            KeyboardState keyboard = Keyboard.GetState();

            var underMouse = GetVoxelUnderMouse();
            // Keep track of whether a new voxel has been selected.
            bool newVoxel = underMouse.IsValid && underMouse != VoxelUnderMouse;

            if (!underMouse.IsValid)
            {
                return;
            }
                        
            VoxelUnderMouse = underMouse;

            // Update the cursor light.
            World.CursorLightPos = underMouse.WorldPosition + new Vector3(0.5f, 0.5f, 0.5f);

            // Get the type of the voxel and display it to the player.
            if (Enabled && !underMouse.IsEmpty && underMouse.IsExplored)
            {
                string info = underMouse.Type.Name;

                // If it belongs to a room, display that information.
                if (World.PlayerFaction.RoomBuilder.IsInRoom(underMouse))
                {
                    Room room = World.PlayerFaction.RoomBuilder.GetMostLikelyRoom(underMouse);

                    if (room != null)
                        info += " (" + room.ID + ")";
                }
                World.ShowInfo(info);
            }

            // Do nothing if not enabled.
            if (!Enabled)
            {
                return;
            }

            bool altPressed = false;
            // If the left or right ALT keys are pressed, we can adjust the height of the selection
            // for building pits and tall walls using the mouse wheel.
            if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
            {
                var change = mouse.ScrollWheelValue - LastMouseWheel;
                BoxYOffset += (change) * 0.01f;
                int offset = (int) BoxYOffset;
                if (offset != PrevBoxYOffsetInt)
                {
                    DragSound.Play(World.CursorLightPos);
                    newVoxel = true;
                }
                PrevBoxYOffsetInt = offset;
                LastMouseWheel = mouse.ScrollWheelValue;
                altPressed = true;
            }
            else
            {
                LastMouseWheel = mouse.ScrollWheelValue;
            }

            // Draw a box around the current voxel under the mouse.
            if (underMouse.IsValid)
            {
                BoundingBox box = underMouse.GetBoundingBox().Expand(0.05f);
                Drawer3D.DrawBox(box, CurrentColor, CurrentWidth, true);
            }

            // If the left mouse button is pressed, update the slection buffer.
            if (isLeftPressed)
            {
                // On release, select voxels.
                if (mouse.LeftButton == ButtonState.Released)
                {
                    ReleaseSound.Play(World.CursorLightPos);
                    isLeftPressed = false;
                    LeftReleasedCallback();
                    BoxYOffset = 0;
                    PrevBoxYOffsetInt = 0;
                }
                // Otherwise, update the selection buffer
                else
                {
                    if (SelectionBuffer.Count == 0)
                    {
                        FirstVoxel = underMouse;
                        SelectionBuffer.Add(underMouse);
                    }
                    else
                    {
                        SelectionBuffer.Clear();
                        SelectionBuffer.Add(FirstVoxel);
                        SelectionBuffer.Add(underMouse);
                        BoundingBox buffer = GetSelectionBox();

                        // Update the selection box to account for offsets from mouse wheel.
                        if (BoxYOffset > 0)
                        {
                            buffer.Max.Y += BoxYOffset;
                        }
                        else if (BoxYOffset < 0)
                        {
                            buffer.Min.Y += BoxYOffset;
                        }

                        SelectionBuffer = Select(buffer, FirstVoxel.WorldPosition, underMouse.WorldPosition).ToList();

                        if (!altPressed && Brush != VoxelBrush.Stairs)
                        {
                            if (SelectionType == VoxelSelectionType.SelectFilled)
                                SelectionBuffer.RemoveAll(v =>
                                {
                                    if (v.Equals(underMouse)) return false;
                                    return !VoxelHelpers.DoesVoxelHaveVisibleSurface(
                                        Chunks.ChunkData, v);
                                });
                        }

                        if (newVoxel)
                        {
                            DragSound.Play(World.CursorLightPos, SelectionBuffer.Count / 20.0f);
                            Dragged.Invoke(SelectionBuffer, InputManager.MouseButton.Left);
                        }
                    }
                }
            }
            // If the mouse was not previously pressed, but is now pressed, then notify us of that.
            else if (mouse.LeftButton == ButtonState.Pressed)
            {
                ClickSound.Play(World.CursorLightPos); ;
                isLeftPressed = true;
                BoxYOffset = 0;
                PrevBoxYOffsetInt = 0;
            }

            // Case where the right mouse button is pressed (mirrors left mouse button)
            // TODO(Break this into a function)
            if (isRightPressed)
            {
                if (mouse.RightButton == ButtonState.Released)
                {
                    ReleaseSound.Play(World.CursorLightPos);
                    isRightPressed = false;
                    RightReleasedCallback();
                    BoxYOffset = 0;
                    PrevBoxYOffsetInt = 0;
                }
                else
                {
                    if (SelectionBuffer.Count == 0)
                    {
                        SelectionBuffer.Add(underMouse);
                        FirstVoxel = underMouse;
                    }
                    else
                    {
                        SelectionBuffer.Clear();
                        SelectionBuffer.Add(FirstVoxel);
                        SelectionBuffer.Add(underMouse);
                        BoundingBox buffer = GetSelectionBox();
                        if (BoxYOffset > 0)
                        {
                            buffer.Max.Y += BoxYOffset;
                        }
                        else if (BoxYOffset < 0)
                        {
                            buffer.Min.Y += BoxYOffset;
                        }

                        SelectionBuffer = VoxelHelpers.EnumerateCoordinatesInBoundingBox(buffer)
                            .Select(c => new TemporaryVoxelHandle(Chunks.ChunkData, c))
                            .Where(v => v.IsValid)
                            .ToList();

                        if (!altPressed && Brush != VoxelBrush.Stairs)
                        {
                            if (SelectionType == VoxelSelectionType.SelectFilled)
                                SelectionBuffer.RemoveAll(v =>
                                {
                                    if (v.Equals(underMouse)) return false;
                                    return !VoxelHelpers.DoesVoxelHaveVisibleSurface(
                                        Chunks.ChunkData, v);
                                });
                        }
                        if (newVoxel)
                        {
                            DragSound.Play(World.CursorLightPos, SelectionBuffer.Count / 20.0f);
                            Dragged.Invoke(SelectionBuffer, InputManager.MouseButton.Right);
                        }
                    }
                }
            }
            else if (mouse.RightButton == ButtonState.Pressed)
            {
                ClickSound.Play(World.CursorLightPos);
                RightPressedCallback();
                BoxYOffset = 0;
                isRightPressed = true;
            }
        }

        public IEnumerable<TemporaryVoxelHandle> Select(BoundingBox buffer, Vector3 start, Vector3 end)
        {
            switch (Brush)
            {
                case VoxelBrush.Box:
                    return VoxelHelpers.EnumerateCoordinatesInBoundingBox(buffer)
                        .Select(c => new TemporaryVoxelHandle(Chunks.ChunkData, c))
                        .Where(v => v.IsValid);
                case VoxelBrush.Shell:
                    return EnumerateShell(buffer)
                            .Select(c => new TemporaryVoxelHandle(Chunks.ChunkData, c))
                            .Where(v => v.IsValid);
                case VoxelBrush.Stairs:
                    return EnumerateStairVoxels(buffer, start, end, SelectionType == VoxelSelectionType.SelectFilled)
                        .Select(c => new TemporaryVoxelHandle(Chunks.ChunkData, c))
                        .Where(v => v.IsValid);
                default:
                    throw new InvalidOperationException("VoxelBrush has invalid value");
            }
        }

        /// <summary>
        /// Gets a stairstep stretching accross the box.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <returns>A stairstep starting filled on the bottom row, pointing in the maximum x or z direction</returns>
        private IEnumerable<GlobalVoxelCoordinate> EnumerateStairVoxels(BoundingBox box,  Vector3 start, Vector3 end, bool invert)
        {
            // Todo: Can this be simplified to return voxels above or below the line?
            int minX = MathFunctions.FloorInt(box.Min.X + 0.5f);
            int minY = MathFunctions.FloorInt(box.Min.Y + 0.5f);
            int minZ = MathFunctions.FloorInt(box.Min.Z + 0.5f);
            int maxX = MathFunctions.FloorInt(box.Max.X - 0.5f);
            int maxY = MathFunctions.FloorInt(box.Max.Y - 0.5f);
            int maxZ = MathFunctions.FloorInt(box.Max.Z - 0.5f);

            // If not inverted, selects the Xs
            // If inverted, selects the Os
            //max y ----xOOOO
            //      --- xxOOO
            //      --- xxxOO
            //      --- xxxxO
            //min y --- xxxxx
            //        minx --- maxx
            float dx = box.Max.X - box.Min.X;
            float dz = box.Max.Z - box.Min.Z;
            Vector3 dir = end - start;
            bool direction = dx > dz;
            bool positiveDir = direction ? dir.X < 0 : dir.Z < 0;
            int step = 0;

            // Always make staircases go exactly to the top or bottom of the selection.
            if (direction && invert)
            {
                minY = maxY - (maxX - minX);
            }
            else if (direction)
            {
                maxY = minY + (maxX - minX);
            }
            else if (invert)
            {
                minY = maxY - (maxZ - minZ);
            }
            else
            {
                maxY = minY + (maxZ - minZ);
            }
            int dy = maxY - minY;
            // Start from the bottom of the stairs up to the top.
            for (int y = minY; y <= maxY; y++)
            {
                int carve = invert ? MathFunctions.Clamp(dy - step, 0, dy) : step;
                // If stairs are in x direction
                if (direction)
                {
                    if (positiveDir)
                    {
                        // Start from min x, and march up to maxY - y
                        for (int x = minX; x <= MathFunctions.Clamp(maxX - carve, minX, maxX); x++)
                        {
                            for (int z = minZ; z <= maxZ; z++)
                            {
                                yield return new GlobalVoxelCoordinate(x, y, z);
                            }
                        }
                    }
                    else
                    {
                        // Start from min x, and march up to maxY - y
                        for (int x = maxX; x >= MathFunctions.Clamp(minX + carve, minX, maxX); x--)
                        {
                            for (int z = minZ; z <= maxZ; z++)
                            {
                                yield return new GlobalVoxelCoordinate(x, y, z);
                            }
                        }
                    }
                    step++;
                }
                // Otherwise, they are in the z direction.
                else
                {
                    if (positiveDir)
                    {
                        // Start from min z, and march up to maxY - y
                        for (int z = minZ; z <= MathFunctions.Clamp(maxZ - carve, minZ, maxZ); z++)
                        {
                            for (int x = minX; x <= maxX; x++)
                            {
                                yield return new GlobalVoxelCoordinate(x, y, z);
                            }
                        }
                    }
                    else
                    {
                        // Start from min z, and march up to maxY - y
                        for (int z = maxZ; z >= MathFunctions.Clamp(minZ + carve, minZ, maxZ); z--)
                        {
                            for (int x = minX; x <= maxX; x++)
                            {
                                yield return new GlobalVoxelCoordinate(x, y, z);
                            }
                        }
                    }
                    step++;
                }
            }
        }
        
        /// <summary>
        /// Gets the 1-voxel shell of a bounding box.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <returns>A list of points on the boundary of the box.</returns>
        private IEnumerable<GlobalVoxelCoordinate> EnumerateShell(BoundingBox box)
        {
            int minX = MathFunctions.FloorInt(box.Min.X + 0.5f);
            int minY = MathFunctions.FloorInt(box.Min.Y + 0.5f);
            int minZ = MathFunctions.FloorInt(box.Min.Z + 0.5f);
            int maxX = MathFunctions.FloorInt(box.Max.X - 0.5f);
            int maxY = MathFunctions.FloorInt(box.Max.Y - 0.5f);
            int maxZ = MathFunctions.FloorInt(box.Max.Z - 0.5f);
            
            for (var y = minY; y <= maxY; y++)
            {
                // yx planes
                for (var z = minZ; z <= maxZ; z++)
                {
                    yield return new GlobalVoxelCoordinate(minX, y, z);
                    yield return new GlobalVoxelCoordinate(maxX, y, z);
                }
                // yz planes
                for (var x = minX + 1; x < maxX; x++)
                {
                    yield return new GlobalVoxelCoordinate(x, y, minZ);
                    yield return new GlobalVoxelCoordinate(x, y, maxZ);
                }
            }

        }

        public void DraggedCallback(List<TemporaryVoxelHandle> voxels, InputManager.MouseButton button)
        {
        }


        public void SelectedCallback(List<TemporaryVoxelHandle> voxels, InputManager.MouseButton button)
        {
        }

        public BoundingBox GetSelectionBox(float expansion)
        {
            List<BoundingBox> aabbs = (from voxel in SelectionBuffer
                                       where voxel != null
                                       select voxel.GetBoundingBox()).ToList();

            BoundingBox superset = MathFunctions.GetBoundingBox(aabbs).Expand(expansion);

            return superset;
        }

        public BoundingBox GetSelectionBox()
        {
            List<BoundingBox> aabbs = (from voxel in SelectionBuffer
                                       where voxel != null
                                       select voxel.GetBoundingBox()).ToList();

            BoundingBox superset = MathFunctions.GetBoundingBox(aabbs);

            return superset;
        }

        public void Render()
        {
            if (SelectionBuffer.Count <= 0)
            {
                return;
            }

            BoundingBox superset = GetSelectionBox(0.1f);

            Drawer3D.DrawBox(superset, Mouse.GetState().LeftButton == ButtonState.Pressed ? SelectionColor : DeleteColor,
                SelectionWidth, false);

            var screenRect = new Rectangle(0, 0, 5, 5);
            Vector3 half = Vector3.One * 0.5f;
            Color dotColor = Mouse.GetState().LeftButton == ButtonState.Pressed ? SelectionColor : DeleteColor;
            dotColor.A = 90;
            foreach (var v in SelectionBuffer)
            {
                if (!v.IsValid) continue;
                
                if ((SelectionType == VoxelSelectionType.SelectFilled && !v.IsEmpty)
                    || (SelectionType == VoxelSelectionType.SelectEmpty && v.IsEmpty))
                {
                    Drawer2D.DrawRect(World.Camera, v.WorldPosition + half, screenRect, dotColor, Color.Transparent, 0.0f);
                }
            }
        }

        public TemporaryVoxelHandle GetVoxelUnderMouse()
        {
            MouseState mouse = Mouse.GetState();

            var v = VoxelHelpers.FindFirstVisibleVoxelOnScreenRay(
                Chunks.ChunkData,
                mouse.X,
                mouse.Y,
                CameraController,
                Graphics.Viewport,
                150.0f,
                SelectionType == VoxelSelectionType.SelectEmpty,
                null);

            if (!v.IsValid)
                return TemporaryVoxelHandle.InvalidHandle;

            switch (SelectionType)
            {
                case VoxelSelectionType.SelectFilled:
                    if (!v.IsEmpty)
                        return v;
                    return TemporaryVoxelHandle.InvalidHandle;
                case VoxelSelectionType.SelectEmpty:
                    return v;
            }

            return TemporaryVoxelHandle.InvalidHandle;
        }

        public TemporaryVoxelHandle LeftPressedCallback()
        {
            SelectionBuffer.Clear();
            return GetVoxelUnderMouse();
        }

        public TemporaryVoxelHandle RightPressedCallback()
        {
            SelectionBuffer.Clear();
            return GetVoxelUnderMouse();
        }

        public List<TemporaryVoxelHandle> LeftReleasedCallback()
        {
            var toReturn = new List<TemporaryVoxelHandle>();
            if (SelectionBuffer.Count > 0)
            {
                toReturn.AddRange(SelectionBuffer);
                SelectionBuffer.Clear();
                Selected.Invoke(toReturn, InputManager.MouseButton.Left);
            }
            return toReturn;
        }

        public List<TemporaryVoxelHandle> RightReleasedCallback()
        {
            var toReturn = new List<TemporaryVoxelHandle>();
            toReturn.AddRange(SelectionBuffer);
            SelectionBuffer.Clear();
            Selected.Invoke(toReturn, InputManager.MouseButton.Right);
            return toReturn;
        }
    }
}
