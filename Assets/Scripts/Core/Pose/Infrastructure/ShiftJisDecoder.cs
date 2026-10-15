using System;
using System.Text;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    public static class ShiftJisDecoder
    {
        private static Encoding _shiftJis;

        public static Encoding GetEncoding()
        {
            if (_shiftJis == null)
            {
                /*
                // Note: requires I18N.CJK.dll to be included in Unity build, otherwise it will fail or fallback.
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // for newer .NET, if needed
                }
                catch { }
                */

                try
                {
                    _shiftJis = Encoding.GetEncoding(932);
                }
                catch (NotSupportedException)
                {
                    UnityEngine.Debug.LogWarning("[Pose] Shift-JIS encoding is not supported. Falling back to UTF8. Please ensure I18N.CJK is preserved in link.xml.");
                    _shiftJis = Encoding.UTF8;
                }
            }
            return _shiftJis;
        }

        public static string GetString(byte[] bytes, int index, int count)
        {
            return GetEncoding().GetString(bytes, index, count);
        }
    }
}
