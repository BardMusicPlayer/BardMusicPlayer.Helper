/*
 * Copyright(c) 2021 MoogleTroupe, 2018-2020 parulina
 * Licensed under the GPL v3 license. See https://github.com/BardMusicPlayer/BardMusicPlayer.Helper/blob/develop/LICENSE for full license information.
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace BardMusicPlayer.Helper.Dalamud
{
    [StructLayout(LayoutKind.Explicit)]
    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    internal readonly struct ChatLine : IDisposable
    {
        [FieldOffset(0)] private readonly IntPtr pointer;

        [FieldOffset(16)] private readonly ulong length;

        [FieldOffset(8)] private readonly ulong unused1;

        [FieldOffset(24)] private readonly ulong unused2;

        internal ChatLine(string message)
        {
            message ??= "";
            
            if (!string.IsNullOrEmpty(message) && Encoding.UTF8.GetByteCount(message) > 500)
            {
            
                var sb = new StringBuilder();
                var byteCounter = 0;
                var enumerator = StringInfo.GetTextElementEnumerator(message);
                
                while (enumerator.MoveNext())
                {
                    var textElement = enumerator.GetTextElement();
                    byteCounter += Encoding.UTF8.GetByteCount(textElement);
                    if (byteCounter <= 500)
                    {
                        sb.Append(textElement);
                    }
                    else
                    {
                        break;
                    }
                }
                
                message = sb.ToString();
            }

            unused1 = 64uL;
            unused2 = 0uL;

            var messageBytes = Encoding.UTF8.GetBytes(message);

            pointer = Marshal.AllocHGlobal(messageBytes.Length + 30);
            Marshal.Copy(messageBytes, 0, pointer, messageBytes.Length);
            Marshal.WriteByte(pointer + messageBytes.Length, 0);

            length = (ulong) (messageBytes.Length + 1);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
