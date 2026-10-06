using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace VideoMosaic;

internal static class SourceFileActions
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHParseDisplayName(string name, nint bindingContext, out nint item, uint attributes, out uint resultAttributes);
    [DllImport("shell32.dll")]
    static extern int SHOpenFolderAndSelectItems(nint item, uint count, nint children, uint flags);

    internal static string ExistingPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException(Language.T("元のファイルが見つかりません。移動または削除されている可能性があります。"), fullPath);
        return fullPath;
    }

    internal static nint ParseItem(string path)
    {
        Application.OleRequired();
        int result = SHParseDisplayName(ExistingPath(path), 0, out nint item, 0, out _);
        if (result < 0)
        {
            if (item != 0) Marshal.FreeCoTaskMem(item);
            Marshal.ThrowExceptionForHR(result);
        }
        return item;
    }

    internal static DataObject CopyData(string path)
    {
        var data = new DataObject();
        data.SetFileDropList(new StringCollection { ExistingPath(path) });
        // CF_HDROP transfers the original file, while DROPEFFECT_COPY prevents a move on paste.
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(1)));
        return data;
    }

    public static void ShowInExplorer(IWin32Window owner, string path) => Perform(owner, path, "元のファイルの場所を開けませんでした。", () =>
    {
        nint item = ParseItem(path);
        try { Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, 0, 0)); }
        finally { Marshal.FreeCoTaskMem(item); }
    });

    public static void Copy(IWin32Window owner, string path) => Perform(owner, path, "ファイルをコピーできませんでした。少し待ってから再度お試しください。", () =>
    {
        // Flush the OLE clipboard so Explorer can paste even after Möbius exits.
        var data = CopyData(path);
        using var effect = (MemoryStream)data.GetData("Preferred DropEffect")!;
        Clipboard.SetDataObject(data, true, 5, 50);
    });

    static void Perform(IWin32Window owner, string path, string error, Action action)
    {
        try { action(); }
        catch (FileNotFoundException)
        {
            MessageBox.Show(owner, Language.T("元のファイルが見つかりません。移動または削除されている可能性があります。") + "\n\n" + path, "Möbius", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, Language.T(error) + "\n\n" + ex.Message, "Möbius", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
