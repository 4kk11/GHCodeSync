import * as vscode from 'vscode';
import * as WebSocket from 'ws';

/**
 * GrasshopperClientクラス
 * Grasshopperとの WebSocket 通信を管理し、スクリプトの同期を行う
 * - WebSocketサーバー（Grasshopper側）との接続管理
 * - スクリプトの送受信処理
 * - VSCode UI（ステータスバー等）の更新
 */
class GrasshopperClient {
    setComponentGuid(filePath: string, guid: string): void {
        this.fileGuidMap.set(filePath, guid);
        vscode.window.showInformationMessage(`ComponentのGUIDを設定しました: ${guid}`);
    }

    /** WebSocket接続インスタンス */
    private socket: WebSocket | null = null;
    /** VSCodeのステータスバーアイテム */
    private statusBarItem: vscode.StatusBarItem;
    /** 現在監視中のエディタ */
    private currentEditor?: vscode.TextEditor;
    /** ファイルとコンポーネントGUIDの紐付け */
    private fileGuidMap: Map<string, string> = new Map();
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
            vscode.window.onDidChangeActiveTextEditor(editor => {
                this.handleEditorChange(editor);
            })
        );

        // ドキュメントの保存時の処理
        context.subscriptions.push(
            vscode.workspace.onDidSaveTextDocument(document => {
                this.handleDocumentSave(document);
            })
        );

        // 初期アクティブエディタの処理
        this.handleEditorChange(vscode.window.activeTextEditor);
    }

    /**
     * エディタ変更時の処理
     * @param editor 新しいアクティブエディタ
     */
    private async handleEditorChange(editor?: vscode.TextEditor): Promise<void> {
        this.currentEditor = editor;
        if (editor && editor.document.languageId === 'csharp') {
            const filePath = editor.document.uri.fsPath;
            console.log('File Path:', filePath);

            // ファイルパスから一時ディレクトリのパスを取得
            const tempDir = filePath.substring(0, filePath.lastIndexOf('/'));
            console.log('Temp Dir:', tempDir);

            try {
                // connect.cmdファイルを読み込む
                const connectCmdPath = `${tempDir}/connect.cmd`;
                const fs = require('fs');
                if (fs.existsSync(connectCmdPath)) {
                    const connectCmd = JSON.parse(fs.readFileSync(connectCmdPath, 'utf8'));
                    console.log('Connect CMD:', connectCmd);

                    if (connectCmd.guid && !this.fileGuidMap.has(filePath)) {
                        // GUIDを設定して接続
                        this.setComponentGuid(filePath, connectCmd.guid);
                        this.connect();
                    }
                } else {
                    // connect.cmdが見つからない場合は手動設定を提案
                    if (!this.fileGuidMap.has(filePath)) {
                        this.promptForComponentGuid();
                    }
                }
            } catch (error) {
                console.error('Error reading connect.cmd:', error);
                // エラーの場合は手動設定を提案
                if (!this.fileGuidMap.has(filePath)) {
                    this.promptForComponentGuid();
                }
            }
        }
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
                const guid = this.fileGuidMap.get(document.uri.fsPath);
                if (guid) {
                    await this.sendScriptUpdate(guid, document.getText());
                } else {
                    // GUIDが未設定の場合は設定を提案
                    await this.promptForComponentGuid();
                }
            }
        } catch (error) {
            vscode.window.showErrorMessage(`スクリプトの更新に失敗しました: ${error}`);
        } finally {
            this.isSaving = false;
        }
    }

    /**
     * コンポーネントGUIDの入力を促す
     */
    private async promptForComponentGuid(): Promise<void> {
        const guid = await vscode.window.showInputBox({
            prompt: 'GrasshopperコンポーネントのGUIDを入力してください',
            placeHolder: '例: 810aa6cf-cbff-4d78-bfed-5f899334ef72'
        });

        if (guid && this.currentEditor) {
            this.fileGuidMap.set(this.currentEditor.document.uri.fsPath, guid);
            vscode.window.showInformationMessage(`ComponentのGUIDを設定しました: ${guid}`);
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
}

/**
 * 拡張機能の非アクティブ化時に呼び出される
 */
export function deactivate() {
    if (client) {
        client.disconnect();
    }
}