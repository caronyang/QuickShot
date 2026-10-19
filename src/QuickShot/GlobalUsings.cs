// This project uses both the WPF and the WinForms SDKs (WinForms only for the tray icon),
// so the implicit usings bring System.Windows.Forms and System.Drawing into every file
// alongside the WPF namespaces. The aliases below make the WPF type win wherever both
// namespaces offer the same simple name, and the missing BCL namespaces are added back.

global using System.IO;
global using System.Threading;

global using Point = System.Windows.Point;
global using Rect = System.Windows.Rect;
global using Size = System.Windows.Size;
global using Application = System.Windows.Application;
global using Binding = System.Windows.Data.Binding;
global using Clipboard = System.Windows.Clipboard;
global using MessageBox = System.Windows.MessageBox;
global using Cursor = System.Windows.Input.Cursor;
global using Image = System.Windows.Controls.Image;
global using Color = System.Windows.Media.Color;
global using FontFamily = System.Windows.Media.FontFamily;
global using PixelFormat = System.Drawing.Imaging.PixelFormat;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using DpiChangedEventArgs = System.Windows.DpiChangedEventArgs;
