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
    static class PacketMarshal {

        /// <summary>
        /// Generically will marshal a structure from a supplied byte buffer.
        /// </summary>
        /// <typeparam name="T">The type of structure to return</typeparam>
        /// <param name="buffer">The input byte buffer</param>
        /// <returns>A populated T where T is the structure supplied. :)</returns>
        public static T MarshalFromBuffer<T>(byte[] buffer) where T : struct {
            T packetStruct;
            int size = Marshal.SizeOf(new T());
            IntPtr heapAlloc = Marshal.AllocHGlobal(size);

            try {
                //copy from the heap
                Marshal.Copy(buffer, 0, heapAlloc, size);

                packetStruct = (T)Marshal.PtrToStructure(heapAlloc, typeof(T));
            }
            finally {
                //free HGLOBAL.  we do not want memory leaks.
                Marshal.FreeHGlobal(heapAlloc);
            }

            return packetStruct;
        }

    }
}
