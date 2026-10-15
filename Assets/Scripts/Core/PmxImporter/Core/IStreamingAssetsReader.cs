using System.Threading.Tasks;

namespace MMDPlayerForVR.Pose.Application
{
    /// <summary>
    /// ファイルの非同期読み込みを抽象化するインターフェース。
    ///
    /// <para><b>コア側が期待する契約:</b>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>relativePath</c> はプラットフォームに依存しない相対パスまたは絶対パスを受け取る。
    ///     パスの解決（StreamingAssetsへの展開・Android APK内パスへの変換など）は実装クラスが担う。
    ///   </description></item>
    ///   <item><description>
    ///     <c>ReadAllBytesAsync</c> は読み込んだバイト列を返す。
    ///     ファイルが存在しない・読み込みに失敗した場合は <c>null</c> を返す（例外は投げない）。
    ///   </description></item>
    ///   <item><description>
    ///     <c>ExistsAsync</c> はファイルの存在を返す。
    ///     Android APK内の StreamingAssets に対しては HEAD リクエストを使用するため、
    ///     false でも実際には存在する場合がある点に注意（呼び出し元は存在確認失敗を警告として扱うこと）。
    ///   </description></item>
    ///   <item><description>
    ///     いずれのメソッドも非同期（<c>Task</c>）。Unity メインスレッドからの呼び出しを想定している。
    ///   </description></item>
    /// </list>
    /// </para>
    ///
    /// <para><b>実装クラス一覧:</b>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>AsyncFileLoader</c>（層B: Android対応, <c>Assets/Scripts/Android/AsyncFileLoader.cs</c>）:
    ///     Android 実機 / エディタ両対応。StreamingAssets パスを <c>Application.streamingAssetsPath</c> で解決し、
    ///     <c>jar:file://</c> URL に対しては <c>UnityWebRequest</c> を使用する。
    ///     絶対パス（SAF 経由のキャッシュファイル等）はそのまま <c>File.ReadAllBytes</c> で読む。
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IStreamingAssetsReader
    {
        /// <summary>
        /// 指定パスのファイルを非同期で読み込み、バイト列を返す。
        /// 失敗した場合は <c>null</c> を返す。
        /// </summary>
        /// <param name="path">読み込むファイルのパス（相対または絶対）。</param>
        Task<byte[]> ReadAllBytesAsync(string path);

        /// <summary>
        /// 指定パスのファイルが存在するかを非同期で確認する。
        /// </summary>
        /// <param name="path">確認するファイルのパス（相対または絶対）。</param>
        Task<bool> ExistsAsync(string path);
    }
}
