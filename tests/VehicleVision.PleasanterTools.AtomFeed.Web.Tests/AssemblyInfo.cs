// **テストクラスを並列に走らせない。** ParameterFilesTests はプロセス全体の環境変数を書き換えるため、
// 並列だと、同時に起動している別のテスト（アプリは環境変数も読む）の設定へ紛れ込む（実際に踏んだ）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// **テストでは鍵を自動生成しない。** 起動時に Security.local.json を実際の置き場へ書いてしまい、
// 「鍵が無い」状態を試すテストも成り立たなくなる（SecretKeyBootstrapTests は一時の置き場で確かめる）
internal static class DisableSecretKeyAutoGenerate
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Run() =>
        Environment.SetEnvironmentVariable("ATOMFEED_SECRET_KEY_AUTOGENERATE", "false");
}
