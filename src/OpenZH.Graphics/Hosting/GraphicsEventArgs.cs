﻿using System;

namespace OpenZH.Graphics.Hosting
{
    public sealed class GraphicsEventArgs : EventArgs
    {
        public GraphicsDevice GraphicsDevice { get; }
        public SwapChain SwapChain { get; }

        public GraphicsEventArgs(GraphicsDevice graphicsDevice, SwapChain swapChain)
        {
            GraphicsDevice = graphicsDevice;
            SwapChain = swapChain;
        }
    }
}
