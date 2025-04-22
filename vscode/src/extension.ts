import * as vscode from 'vscode';
import * as WebSocket from 'ws';
import * as path from 'path';
import * as fs   from 'fs';
/**
 * GrasshopperClientクラス
 * Grasshopperとの WebSocket 通信を管理し、スクリプトの同期を行う
 * - WebSocketサーバー（Grasshopper側）との接続管理
 * - スクリプトの送受信処理
 * - VSCode UI（ステータスバー等）の更新
 */
class GrasshopperClient {

    /** WebSocket接続インスタンス */
    private socket: WebSocket | null = null;
    /** VSCodeのステータスバーアイテム */
    private statusBarItem: vscode.StatusBarItem;
    /** 現在監視中のエディタ */
    private currentEditor?: vscode.TextEditor;
    /** 保存中フラグ（重複処理防止用） */
    private isSaving: boolean = false;

    constructor() {
        // ステータスバーアイテムを初期化
        this.statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right);
        this.updateStatusBar('未接続', '$(circle-slash)');
    }

    /**
     * エディタの監視を開始
     * @param context 拡張機能のコンテキスト
     */
    startEditorWatching(context: vscode.ExtensionContext): void {
        // アクティブエディタが変更された時の処理
        context.subscriptions.push(
            vscode.window.onDidChangeActiveTextEditor(() => {
                if (!this.socket) {
                    this.connect();
                }
            })
        );

        // ドキュメントの保存時の処理
        context.subscriptions.push(
            vscode.workspace.onDidSaveTextDocument(document => {
                this.handleDocumentSave(document);
            })
        );

        // 初期アクティブエディタの処理
        this.connect();
    }

    /**
     * ドキュメント保存時の処理
     * @param document 保存されたドキュメント
     */
    private async handleDocumentSave(document: vscode.TextDocument): Promise<void> {
        // 重複処理の防止
        if (this.isSaving) return;
        this.isSaving = true;

        try {
            // C#ファイルの場合のみ処理
            if (document.languageId === 'csharp') {
                // ファイルの名前 = GUID
                const fileName = path.basename(document.fileName);
                const guid = fileName.substring(0, fileName.lastIndexOf('.'));
                await this.sendScriptUpdate(guid, document.getText());
            }
        } catch (error) {
            vscode.window.showErrorMessage(`スクリプトの更新に失敗しました: ${error}`);
        } finally {
            this.isSaving = false;
        }
    }

    /**
     * Grasshopper（WebSocketサーバー）への接続を開始
     */
    connect(): void {
        try {
            this.socket = new WebSocket('ws://localhost:8080');
            
            this.socket.on('open', () => {
                this.updateStatusBar('接続済み', '$(check)');
                vscode.window.showInformationMessage('Grasshopperに接続しました');
            });

            this.socket.on('message', (data: WebSocket.RawData) => {
                this.handleMessage(data);
            });

            this.socket.on('error', (error: Error) => {
                vscode.window.showErrorMessage(`接続エラー: ${error.message}`);
                this.updateStatusBar('エラー', '$(error)');
            });

            this.socket.on('close', () => {
                this.updateStatusBar('未接続', '$(circle-slash)');
                vscode.window.showInformationMessage('Grasshopperから切断されました');
            });
        } catch (error) {
            vscode.window.showErrorMessage(`接続エラー: ${error}`);
            this.updateStatusBar('エラー', '$(error)');
        }
    }

    /**
     * Grasshopperとの接続を切断
     */
    disconnect(): void {
        if (this.socket) {
            this.socket.close();
            this.socket = null;
        }
    }

    /**
     * ステータスバーの表示を更新
     */
    private updateStatusBar(text: string, icon: string): void {
        this.statusBarItem.text = `${icon} Grasshopper: ${text}`;
        this.statusBarItem.show();
    }

    /**
     * Grasshopperからのメッセージを処理
     */
    private handleMessage(data: WebSocket.RawData): void {
        try {
            const message = JSON.parse(data.toString());
            switch (message.type) {
                case 'scriptUpdated':
                    vscode.window.showInformationMessage('スクリプトが更新されました');
                    break;
                case 'error':
                    vscode.window.showErrorMessage(`Grasshopperエラー: ${message.message}`);
                    break;
                default:
                    console.log('未知のメッセージタイプ:', message.type);
            }
        } catch (error) {
            console.error('メッセージの解析に失敗:', error);
        }
    }

    /**
     * スクリプトの更新をGrasshopper側に送信
     */
    async sendScriptUpdate(componentId: string, code: string): Promise<void> {
        if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
            throw new Error('Grasshopperに接続されていません');
        }

        const message = {
            type: 'setScript',
            target: componentId,
            code: code,
            language: 'csharp'
        };

        return new Promise((resolve, reject) => {
            try {
                this.socket?.send(JSON.stringify(message), (error) => {
                    if (error) {
                        reject(error);
                    } else {
                        resolve();
                    }
                });
            } catch (error) {
                reject(error);
            }
        });
    }
}

/** グローバルなクライアントインスタンス */
let client: GrasshopperClient;

/**
 * 拡張機能のアクティベーション時に呼び出される
 */
export function activate(context: vscode.ExtensionContext) {
    client = new GrasshopperClient();
    vscode.window.showInformationMessage('Grasshopper拡張機能がアクティブになりました');
    // エディタの監視を開始
    client.startEditorWatching(context);

    // コマンドの登録
    let connectDisposable = vscode.commands.registerCommand('vscode-grasshopper.connect', () => {
        client.connect();
    });

    let disconnectDisposable = vscode.commands.registerCommand('vscode-grasshopper.disconnect', () => {
        client.disconnect();
    });

    let helthCheckDisposable = vscode.commands.registerCommand('vscode-grasshopper.healthCheck', () => {
        vscode.window.showInformationMessage('Grasshopperの状態を確認しました!');
    });

    context.subscriptions.push(connectDisposable);
    context.subscriptions.push(disconnectDisposable);
    context.subscriptions.push(helthCheckDisposable);

    const pattern = new vscode.RelativePattern(vscode.workspace.workspaceFolders![0], '**/*.cmd');
    const folderWatcher = vscode.workspace.createFileSystemWatcher(pattern, false, false, true);

    folderWatcher.onDidChange(async (uri) => {
        vscode.window.showInformationMessage(`ファイルが変更されました: ${uri.fsPath}`);
        console.log('File changed:', uri.fsPath);

        try {
            const connectCmd = getConnectCmd(uri.fsPath);
            if (!connectCmd) throw new Error('connect.cmd not found');

            // .cs ファイル
            const csPath = path.join(path.dirname(uri.fsPath), `${connectCmd.guid}.cs`);
            if (!fs.existsSync(csPath)) throw new Error(`C# file not found: ${csPath}`);

            // ① ドキュメントを開く
            const doc = await vscode.workspace.openTextDocument(vscode.Uri.file(csPath));
      
            // ② エディタ（新しいタブ）で表示
            await vscode.window.showTextDocument(doc, {
              preview: false,                 // プレビューではなく固定タブ
              viewColumn: vscode.ViewColumn.Active
            });
      
          } catch (err) {
            // まれに「まだ書き込み中で開けない」ケースがあるので
            // 必要ならリトライ処理を挟む
            console.error('Failed to open new file:', err);
          }
    });
}


/**
 * connect.cmdファイルからフィールドを取得するヘルパー関数
 */
interface ConnectCmd {
    command: string;
    guid: string;
}

function getConnectCmd(filePath: string): ConnectCmd | null {
    if (!filePath) throw new Error('filePath is required');
    if (!fs.existsSync(filePath)) return null;

    const connectCmd = JSON.parse(fs.readFileSync(filePath, 'utf8'));

    if (!connectCmd) throw new Error('connect.cmd is empty or invalid');
    if (!connectCmd.command) throw new Error('command is required');
    if (!connectCmd.guid) throw new Error('guid is required');

    return connectCmd;
}

/**
 * 拡張機能の非アクティブ化時に呼び出される
 */
export function deactivate() {
    if (client) {
        client.disconnect();
    }
}