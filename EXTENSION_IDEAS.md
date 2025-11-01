# GHCodeSync 拡張の方向性アイデア

## プロジェクト概要

GHCodeSyncは、GrasshopperのC#スクリプトコンポーネントをVSCodeで編集可能にする統合ツールです。WebSocketベースの双方向通信により、VSCodeの強力な開発機能をGrasshopperスクリプト開発に活用できます。

### 現在の主要機能
- WebSocket通信による双方向同期
- リアルタイムコード更新（保存時に自動反映）
- IDE用コード変換（IntelliSense用のダミーメンバー、名前空間注入）
- NuGetパッケージ参照管理（#rディレクティブ）
- .csprojファイルの自動生成と管理

### 現在の制限事項
- C#のみサポート（Python未対応）
- 単一ユーザーのみ（コラボレーション機能なし）
- リアルタイムデバッグ機能なし
- コンパイルエラーのリアルタイムフィードバックなし
- バージョン管理統合なし
- テスト/プロファイリング機能なし

---

## 拡張の方向性アイデア

### 1. 🐍 Python/IronPythonサポートの追加

#### 背景
競合ツール「Script-sync」はPython（IronPythonとCPython）をサポートしており、Grasshopperコミュニティでは両言語が広く使用されています。

#### 実装内容
- **IronPython対応**: Grasshopper標準のIronPythonスクリプトコンポーネント統合
- **CPython対応**: CPythonベースのスクリプトコンポーネント対応（ghpythonlibサポート）
- **言語検出**: コンポーネントタイプに応じた自動言語判別
- **Python用コード変換**: Python用のIntelliSenseヘルパー注入
- **パッケージ管理**: pipベースのパッケージ依存関係管理

#### 技術的アプローチ
```typescript
// VSCode extension側
interface ScriptLanguage {
  type: 'csharp' | 'ironpython' | 'cpython';
  version?: string;
}

// Grasshopper plugin側（C#）
private string DetectScriptLanguage(IGH_DocumentObject component) {
  var typeFullName = component.GetType().FullName;
  if (typeFullName.Contains("PythonComponent")) return "python";
  if (typeFullName.Contains("ScriptComponent")) return "csharp";
  // ...
}
```

#### 期待される効果
- ユーザーベースの拡大（Python開発者を含む）
- Script-syncとの差別化
- より幅広いGrasshopperワークフローのサポート

---

### 2. 🔍 Language Server Protocol (LSP) 統合

#### 背景
現在はダミーメンバーの注入による簡易的なIntelliSenseのみ。LSPを統合することで、より高度なコード補完、エラー診断、リファクタリング機能を提供できます。

#### 実装内容
- **Roslyn-based LSP Server**: OmniSharpまたはcsharp-language-serverの統合
- **リアルタイム型チェック**: Grasshopper/RhinoCommon APIの型情報提供
- **コード診断**: コンパイル前のエラー・警告表示
- **Go to Definition**: Grasshopper APIのドキュメントへのジャンプ
- **リファクタリング**: 変数名変更、メソッド抽出などの支援

#### 技術的アプローチ
```typescript
// package.json
{
  "dependencies": {
    "vscode-languageclient": "^9.0.0",
    "@omniSharp/csharp-language-server-protocol": "latest"
  }
}

// LSP統合
import { LanguageClient, LanguageClientOptions } from 'vscode-languageclient/node';

class GrasshopperLSPClient {
  private client: LanguageClient;

  async start(context: vscode.ExtensionContext) {
    const serverOptions = {
      // Roslyn-based language serverの設定
      command: 'csharp-ls',
      args: ['--stdio']
    };

    this.client = new LanguageClient(
      'ghcodesync-lsp',
      'GHCodeSync Language Server',
      serverOptions,
      clientOptions
    );

    await this.client.start();
  }
}
```

#### 期待される効果
- 開発体験の大幅向上
- コーディングエラーの早期発見
- より高度なコード補完とナビゲーション

---

### 3. 🔥 Hot Reload / Live Debuggingサポート

#### 背景
現在は保存時のみ更新。Visual Studio 2022のHot Reload機能のように、デバッグ中にコードを変更して即座に反映できれば、開発サイクルが大幅に短縮されます。

#### 実装内容
- **Hot Reload**: コード変更を保存せずにGrasshopperに反映
- **ブレークポイント**: VSCodeからGrasshopperスクリプトにブレークポイント設定
- **変数インスペクション**: 実行時の変数値の確認
- **Watch式**: カスタム式の評価
- **コールスタック**: エラー発生時のスタックトレース表示

#### 技術的アプローチ
```csharp
// Grasshopper側: デバッグアダプタープロトコル (DAP) サーバー
public class GHDebugAdapter : IDebugAdapter {
  private Dictionary<string, List<int>> breakpoints;

  public void SetBreakpoint(string componentGuid, int lineNumber) {
    // ブレークポイント情報を保存
    if (!breakpoints.ContainsKey(componentGuid)) {
      breakpoints[componentGuid] = new List<int>();
    }
    breakpoints[componentGuid].Add(lineNumber);
  }

  public void OnScriptExecution(string guid, int currentLine) {
    if (breakpoints.ContainsKey(guid) &&
        breakpoints[guid].Contains(currentLine)) {
      // VSCodeに停止を通知
      SendDebugEvent("stopped", new { reason = "breakpoint" });
    }
  }
}
```

```typescript
// VSCode側: Debug Adapter Protocol実装
import { DebugSession } from 'vscode-debugadapter';

export class GrasshopperDebugSession extends DebugSession {
  protected setBreakPointsRequest(
    response: DebugProtocol.SetBreakpointsResponse,
    args: DebugProtocol.SetBreakpointsArguments
  ): void {
    const breakpoints = args.breakpoints || [];
    // WebSocket経由でGrasshopperにブレークポイント情報を送信
    this.client.sendBreakpoints(args.source.path, breakpoints);
  }
}
```

#### 期待される効果
- デバッグ時間の大幅短縮
- 複雑なアルゴリズムのトラブルシューティングが容易に
- プロフェッショナルな開発体験の提供

---

### 4. 👥 マルチユーザーコラボレーション機能

#### 背景
Google Docsのようなリアルタイムコラボレーション機能により、チームでのGrasshopper開発が可能になります。CRDTまたはOTを使用して競合を自動解決します。

#### 実装内容
- **リアルタイム同期**: 複数ユーザーが同時に同じスクリプトを編集
- **カーソル共有**: 他のユーザーのカーソル位置を表示
- **競合解決**: CRDT（Conflict-free Replicated Data Types）による自動マージ
- **ユーザー識別**: アバターと名前によるユーザー識別
- **変更履歴**: 誰がいつ何を変更したかの追跡

#### 技術的アプローチ
```typescript
// Yjs (CRDT library) の使用
import * as Y from 'yjs';
import { WebsocketProvider } from 'y-websocket';

class CollaborationManager {
  private yDoc: Y.Doc;
  private provider: WebsocketProvider;
  private yText: Y.Text;

  initCollaboration(componentId: string) {
    this.yDoc = new Y.Doc();
    this.yText = this.yDoc.getText('code');

    // WebSocketプロバイダー（Grasshopper側に中継サーバー）
    this.provider = new WebsocketProvider(
      `ws://localhost:51234/collab/${componentId}`,
      this.yDoc
    );

    // VSCodeエディタとの双方向バインディング
    this.yText.observe(event => {
      // エディタを更新
      this.updateEditor(this.yText.toString());
    });
  }

  onEditorChange(text: string) {
    // YTextを更新（自動的に他のユーザーに伝播）
    this.yDoc.transact(() => {
      this.yText.delete(0, this.yText.length);
      this.yText.insert(0, text);
    });
  }
}
```

```csharp
// Grasshopper側: YjsサーバーまたはOTサーバー
public class CollaborationServer {
  private Dictionary<string, List<WebSocket>> rooms;

  public void BroadcastChange(string componentId, ChangeSet change) {
    if (rooms.ContainsKey(componentId)) {
      foreach (var client in rooms[componentId]) {
        client.Send(JsonConvert.SerializeObject(change));
      }
    }
  }
}
```

#### 期待される効果
- チーム開発の効率化
- ペアプログラミングのサポート
- 教育シーンでの活用（講師と学生のリアルタイム共有）

---

### 5. 🤖 AI Code Assistantの統合

#### 背景
GitHub Copilotのような生成AI統合により、Grasshopper特有のAPIやパターンの学習コストを下げ、コーディング効率を向上させます。

#### 実装内容
- **GitHub Copilot統合**: Grasshopper/RhinoCommon APIに特化した補完
- **コード説明**: 選択したコードの自然言語説明生成
- **エラー修正提案**: コンパイルエラーの自動修正候補提示
- **コード生成**: 自然言語からGrasshopper C#コードを生成
- **リファクタリング提案**: パフォーマンス改善やベストプラクティス適用の提案

#### 技術的アプローチ
```typescript
// GitHub Copilot Chat API の使用
import * as vscode from 'vscode';

export class GrasshopperAIAssistant {
  async explainCode(code: string): Promise<string> {
    const messages = [
      vscode.LanguageModelChatMessage.User(
        `Explain this Grasshopper C# code:\n${code}`
      )
    ];

    const chatRequest = await vscode.lm.sendChatRequest(
      'copilot-gpt-4',
      messages,
      {},
      new vscode.CancellationTokenSource().token
    );

    let explanation = '';
    for await (const fragment of chatRequest.text) {
      explanation += fragment;
    }
    return explanation;
  }

  async generateCode(prompt: string): Promise<string> {
    const systemPrompt = `You are an expert in Grasshopper C# scripting.
    Generate code using RhinoCommon and Grasshopper API.
    Available context: RhinoDocument, GrasshopperDocument, Component, Iteration.
    Use Print() for debugging output.`;

    const messages = [
      vscode.LanguageModelChatMessage.User(systemPrompt),
      vscode.LanguageModelChatMessage.User(prompt)
    ];

    // ... similar to above
  }
}
```

#### 期待される効果
- 初心者の学習カーブ短縮
- 高度なアルゴリズムの実装支援
- コーディング速度の向上

---

### 6. 🧪 テスティングフレームワーク統合

#### 背景
現在Grasshopperスクリプトのユニットテストは困難です。テストフレームワークを統合することで、コード品質の向上と回帰防止が可能になります。

#### 実装内容
- **ユニットテストサポート**: xUnit/NUnitベースのテストフレームワーク
- **モックデータ生成**: RhinoDocumentやGrasshopperコンポーネントのモック
- **テストカバレッジ**: コードカバレッジレポートの表示
- **TDD支援**: Test Driven Developmentワークフロー
- **自動テスト実行**: ファイル保存時の自動テスト実行

#### 技術的アプローチ
```csharp
// テスト用のヘルパークラス
public class GrasshopperTestHelper {
  public static Mock<RhinoDoc> CreateMockRhinoDoc() {
    var mock = new Mock<RhinoDoc>();
    // 必要なプロパティとメソッドをセットアップ
    return mock;
  }

  public static TestContext CreateTestContext() {
    return new TestContext {
      RhinoDocument = CreateMockRhinoDoc().Object,
      // ...
    };
  }
}

// テストコード例
[Test]
public void TestMyGrasshopperScript() {
  var ctx = GrasshopperTestHelper.CreateTestContext();
  var script = new Script_Instance();

  // テスト実行
  var result = script.RunScript(
    /* inputs */,
    /* outputs */
  );

  Assert.That(result, Is.EqualTo(expectedValue));
}
```

```typescript
// VSCode側: テスト実行とカバレッジ表示
export class TestManager {
  async runTests(componentId: string) {
    // Grasshopper側でテストを実行
    const results = await this.client.runTests(componentId);

    // テストエクスプローラーに結果を表示
    this.testController.items.forEach(item => {
      item.error = results.errors[item.id];
    });

    // カバレッジをエディタに表示
    this.showCoverage(results.coverage);
  }
}
```

#### 期待される効果
- コード品質の向上
- リファクタリングの安全性向上
- 複雑なロジックの検証が容易に

---

### 7. 📊 パフォーマンスプロファイリング機能

#### 背景
Grasshopperスクリプトのパフォーマンス最適化は重要ですが、現在はGH_Document.Profilerの基本機能のみ。より詳細なプロファイリング情報が必要です。

#### 実装内容
- **実行時間分析**: 関数ごとの実行時間測定
- **メモリ使用量**: メモリアロケーションの追跡
- **ボトルネック検出**: 最も時間のかかる部分の自動検出
- **フレームグラフ**: 視覚的なプロファイリング結果表示
- **比較分析**: コード変更前後のパフォーマンス比較

#### 技術的アプローチ
```csharp
// Grasshopper側: プロファイラー
public class GrasshopperProfiler {
  private Dictionary<string, ProfileData> profileResults;

  public void StartProfiling(string componentId) {
    var stopwatch = Stopwatch.StartNew();
    var beforeMemory = GC.GetTotalMemory(false);

    // スクリプト実行
    RunScript(componentId);

    var afterMemory = GC.GetTotalMemory(false);
    stopwatch.Stop();

    profileResults[componentId] = new ProfileData {
      ExecutionTime = stopwatch.ElapsedMilliseconds,
      MemoryUsed = afterMemory - beforeMemory,
      Timestamp = DateTime.Now
    };

    // VSCodeに結果を送信
    SendProfilingResults(componentId, profileResults[componentId]);
  }
}
```

```typescript
// VSCode側: プロファイリング結果の表示
export class ProfilingPanel {
  showResults(data: ProfileData) {
    // Webview でフレームグラフを表示
    this.panel.webview.html = this.generateFlameGraph(data);
  }

  compareResults(before: ProfileData, after: ProfileData) {
    const improvement =
      ((before.executionTime - after.executionTime) / before.executionTime) * 100;

    vscode.window.showInformationMessage(
      `Performance improved by ${improvement.toFixed(2)}%`
    );
  }
}
```

#### 期待される効果
- パフォーマンス問題の早期発見
- 最適化の効果測定が容易に
- 大規模データ処理の効率化

---

### 8. 📚 スニペットライブラリとコードテンプレート

#### 背景
Grasshopper開発でよく使うパターン（ジオメトリ生成、データ構造変換など）をスニペットとして提供することで、開発効率が向上します。

#### 実装内容
- **組み込みスニペット**: よく使うGrasshopper/RhinoCommon APIパターン
- **カスタムスニペット**: ユーザー定義のコードテンプレート
- **スニペット共有**: チーム内でのスニペット共有機能
- **コンテキスト認識**: 現在のコンテキストに応じた適切なスニペット提案
- **ドキュメント付きスニペット**: 使用例とAPIリファレンスへのリンク

#### 技術的アプローチ
```json
// VSCode snippets
{
  "Grasshopper Point3d Array": {
    "prefix": "ghpts",
    "body": [
      "List<Point3d> points = new List<Point3d>();",
      "for (int i = 0; i < ${1:count}; i++) {",
      "  points.Add(new Point3d(${2:x}, ${3:y}, ${4:z}));",
      "}",
      "$0"
    ],
    "description": "Create a list of Point3d objects in Grasshopper"
  },

  "RhinoCommon Brep Creation": {
    "prefix": "ghbrep",
    "body": [
      "// Create a Brep from ${1:description}",
      "Brep brep = ${2:Brep.CreateFromCornerPoints(",
      "  new Point3d(${3:0}, ${4:0}, ${5:0}),",
      "  new Point3d(${6:10}, ${7:0}, ${8:0}),",
      "  new Point3d(${9:10}, ${10:10}, ${11:0}),",
      "  ${12:tolerance}",
      ")};",
      "$0"
    ],
    "description": "Create a Brep geometry in RhinoCommon"
  }
}
```

```typescript
// スニペット提案システム
export class SnippetProvider implements vscode.CompletionItemProvider {
  provideCompletionItems(
    document: vscode.TextDocument,
    position: vscode.Position
  ): vscode.CompletionItem[] {
    const context = this.analyzeContext(document, position);

    // コンテキストに応じたスニペットをフィルタリング
    return this.snippetLibrary
      .filter(s => s.contexts.includes(context))
      .map(s => this.createCompletionItem(s));
  }
}
```

#### 期待される効果
- 繰り返しコードの削減
- ベストプラクティスの普及
- 新規ユーザーの学習支援

---

### 9. 🔗 バージョン管理とスナップショット機能

#### 背景
Grasshopperファイル全体はGitで管理できますが、個々のスクリプトコンポーネントの変更履歴を追跡することが困難です。

#### 実装内容
- **自動スナップショット**: コード変更時の自動バックアップ
- **変更履歴**: タイムライン形式での変更履歴表示
- **差分表示**: バージョン間の差分表示
- **ロールバック**: 以前のバージョンへの簡単な復元
- **Git統合**: スクリプト変更のGitコミット自動化

#### 技術的アプローチ
```typescript
export class VersionControl {
  private snapshots: Map<string, Snapshot[]> = new Map();

  async createSnapshot(componentId: string, code: string) {
    const snapshot: Snapshot = {
      id: uuid(),
      componentId,
      code,
      timestamp: Date.now(),
      hash: this.hashCode(code)
    };

    // ローカルストレージに保存
    const history = this.snapshots.get(componentId) || [];
    history.push(snapshot);
    this.snapshots.set(componentId, history);

    // 自動クリーンアップ（古いスナップショットを削除）
    this.cleanupOldSnapshots(componentId);
  }

  async showTimeline(componentId: string) {
    const history = this.snapshots.get(componentId) || [];

    // QuickPickで履歴を表示
    const selected = await vscode.window.showQuickPick(
      history.map(s => ({
        label: new Date(s.timestamp).toLocaleString(),
        description: s.hash.substring(0, 8),
        snapshot: s
      }))
    );

    if (selected) {
      this.restoreSnapshot(selected.snapshot);
    }
  }
}
```

#### 期待される効果
- 実験的な変更の安全性向上
- 誤った変更からの素早い復旧
- 変更履歴の可視化

---

### 10. 🌐 リモート開発とクラウド同期

#### 背景
リモートワークの普及により、異なるマシン間でのシームレスな開発体験が求められています。

#### 実装内容
- **クラウドストレージ統合**: OneDrive、Google Drive、Dropboxとの同期
- **リモート接続**: リモートマシンのGrasshopperに接続
- **設定同期**: VSCode設定とスニペットのクラウド同期
- **マルチデバイス対応**: 複数デバイス間でのシームレスな切り替え
- **オフライン対応**: オフライン時の変更をオンライン復帰時に同期

#### 技術的アプローチ
```typescript
// クラウド同期マネージャー
export class CloudSyncManager {
  private syncProvider: 'onedrive' | 'gdrive' | 'dropbox';

  async syncToCloud(componentId: string, code: string) {
    const cloudPath = `/GHCodeSync/${componentId}.cs`;

    switch (this.syncProvider) {
      case 'onedrive':
        await this.uploadToOneDrive(cloudPath, code);
        break;
      case 'gdrive':
        await this.uploadToGoogleDrive(cloudPath, code);
        break;
      // ...
    }
  }

  async connectRemoteGrasshopper(host: string, port: number) {
    // リモートWebSocketサーバーに接続
    this.client.connect(`ws://${host}:${port}`);
  }
}
```

#### 期待される効果
- リモートワークの効率化
- デバイス間のシームレスな移行
- データ損失リスクの低減

---

## 優先度と実装の推奨順序

### フェーズ1: 基盤強化（短期: 1-3ヶ月）
1. **LSP統合** - 開発体験の基盤向上
2. **スニペットライブラリ** - 即座に価値を提供
3. **バージョン管理** - 安全性の向上

### フェーズ2: 機能拡張（中期: 3-6ヶ月）
4. **Pythonサポート** - ユーザーベース拡大
5. **AI Code Assistant** - 現代的な開発体験
6. **パフォーマンスプロファイリング** - プロフェッショナル向け機能

### フェーズ3: 高度な機能（長期: 6-12ヶ月）
7. **Hot Reload/Live Debugging** - 技術的に複雑だが高価値
8. **テスティングフレームワーク** - エンタープライズ向け
9. **マルチユーザーコラボレーション** - 最も複雑だが革新的
10. **クラウド同期** - エコシステムの完成

---

## 技術的な実装上の考慮事項

### アーキテクチャ
- **プラグインアーキテクチャ**: 各機能を独立したモジュールとして実装
- **設定可能性**: ユーザーが機能のON/OFFを選択可能に
- **パフォーマンス**: 大規模なGrasshopperドキュメントでも快適に動作
- **後方互換性**: 既存のワークフローを壊さない

### セキュリティ
- **認証**: リモート接続時の認証メカニズム
- **暗号化**: クラウド同期時のデータ暗号化
- **権限管理**: コラボレーション時のアクセス制御

### ユーザビリティ
- **段階的な学習**: 初心者から上級者まで対応
- **ドキュメント**: 各機能の詳細なドキュメント
- **チュートリアル**: インタラクティブなチュートリアル提供

---

## まとめ

GHCodeSyncは既に強固な基盤を持っていますが、上記の拡張によりさらに強力なツールへと進化できます。特にLSP統合、Pythonサポート、Hot Reload/Debuggingは競争優位性を高め、ユーザー体験を大幅に向上させるでしょう。

各機能は独立して実装可能ですが、組み合わせることでシナジー効果が生まれます。例えば、LSP + AI Assistant の組み合わせにより、初心者でも高度なGrasshopper開発が可能になります。

実装の優先順位は、ターゲットユーザー（教育機関 vs プロフェッショナル）とリソース（開発時間と予算）に応じて調整することを推奨します。
