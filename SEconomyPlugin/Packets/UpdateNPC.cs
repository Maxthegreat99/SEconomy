/*
 * This file is part of SEconomy - A server-sided currency implementation
 * Copyright (C) 2013-2014, Tyler Watson <tyler@tw.id.au>
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Wolfje.Plugins.SEconomy.Packets {

    public enum FacingDirectionX : byte {
        Left = 0,
        Right = 1
    }
    public enum FacingDirectionY : byte {
        Up = 0,
        Down = 1
    }


    /// <summary>
    /// The strongly-typed Update NPC packet struct
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UpdateNPC {
        public short NPCSlot;
        public float PositionX;
        public float PositionY;
        public float VelocityX;
        public float VelocityY;
        public short TargetPlayerID;
        public FacingDirectionX FacingDirectionX;
        public FacingDirectionY FacingDirectionY;
        public int Life;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] int[] AI;

        public short Type;
    }

}
