"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.deactivate = exports.activate = void 0;
const vscode = require("vscode");
const WebSocket = require("ws");
/**
 * GrasshopperClientクラス
 * Grasshopperとの WebSocket 通信を管理し、スクリプトの同期を行う
 * - WebSocketサーバー（Grasshopper側）との接続管理
 * - スクリプトの送受信処理
 * - VSCode UI（ステータスバー等）の更新
 */
class GrasshopperClient {
    constructor() {
        /** WebSocket接続インスタンス */
        this.socket = null;
        /** ファイルとコンポーネントGUIDの紐付け */
        this.fileGuidMap = new Map();
        /** 保存中フラグ（重複処理防止用） */
        this.isSaving = false;
        // ステータスバーアイテムを初期化
        this.statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right);
        this.updateStatusBar('未接続', '$(circle-slash)');
    }
    /**
     * エディタの監視を開始
     * @param context 拡張機能のコンテキスト
     */
    startEditorWatching(context) {
        // アクティブエディタが変更された時の処理
        context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
            this.handleEditorChange(editor);
        }));
        // ドキュメントの保存時の処理
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(document => {
            this.handleDocumentSave(document);
        }));
        // 初期アクティブエディタの処理
        this.handleEditorChange(vscode.window.activeTextEditor);
    }
    /**
     * エディタ変更時の処理
     * @param editor 新しいアクティブエディタ
     */
    handleEditorChange(editor) {
        this.currentEditor = editor;
        if (editor) {
            // C#ファイルの場合のみGUIDの設定を提案
            if (editor.document.languageId === 'csharp' && !this.fileGuidMap.has(editor.document.uri.fsPath)) {
                this.promptForComponentGuid();
            }
        }
    }
    /**
     * ドキュメント保存時の処理
     * @param document 保存されたドキュメント
     */
    async handleDocumentSave(document) {
        // 重複処理の防止
        if (this.isSaving)
            return;
        this.isSaving = true;
        try {
            // C#ファイルの場合のみ処理
            if (document.languageId === 'csharp') {
                const guid = this.fileGuidMap.get(document.uri.fsPath);
                if (guid) {
                    await this.sendScriptUpdate(guid, document.getText());
                }
                else {
                    // GUIDが未設定の場合は設定を提案
                    await this.promptForComponentGuid();
                }
            }
        }
        catch (error) {
            vscode.window.showErrorMessage(`スクリプトの更新に失敗しました: ${error}`);
        }
        finally {
            this.isSaving = false;
        }
    }
    /**
     * コンポーネントGUIDの入力を促す
     */
    async promptForComponentGuid() {
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
    connect() {
        try {
            this.socket = new WebSocket('ws://localhost:8080');
            this.socket.on('open', () => {
                this.updateStatusBar('接続済み', '$(check)');
                vscode.window.showInformationMessage('Grasshopperに接続しました');
            });
            this.socket.on('message', (data) => {
                this.handleMessage(data);
            });
            this.socket.on('error', (error) => {
                vscode.window.showErrorMessage(`接続エラー: ${error.message}`);
                this.updateStatusBar('エラー', '$(error)');
            });
            this.socket.on('close', () => {
                this.updateStatusBar('未接続', '$(circle-slash)');
                vscode.window.showInformationMessage('Grasshopperから切断されました');
            });
        }
        catch (error) {
            vscode.window.showErrorMessage(`接続エラー: ${error}`);
            this.updateStatusBar('エラー', '$(error)');
        }
    }
    /**
     * Grasshopperとの接続を切断
     */
    disconnect() {
        if (this.socket) {
            this.socket.close();
            this.socket = null;
        }
    }
    /**
     * ステータスバーの表示を更新
     */
    updateStatusBar(text, icon) {
        this.statusBarItem.text = `${icon} Grasshopper: ${text}`;
        this.statusBarItem.show();
    }
    /**
     * Grasshopperからのメッセージを処理
     */
    handleMessage(data) {
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
        }
        catch (error) {
            console.error('メッセージの解析に失敗:', error);
        }
    }
    /**
     * スクリプトの更新をGrasshopper側に送信
     */
    async sendScriptUpdate(componentId, code) {
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
                    }
                    else {
                        resolve();
                    }
                });
            }
            catch (error) {
                reject(error);
            }
        });
    }
}
/** グローバルなクライアントインスタンス */
let client;
/**
 * 拡張機能のアクティベーション時に呼び出される
 */
function activate(context) {
    client = new GrasshopperClient();
    // エディタの監視を開始
    client.startEditorWatching(context);
    // コマンドの登録
    let connectDisposable = vscode.commands.registerCommand('vscode-grasshopper.connect', () => {
        client.connect();
    });
    let disconnectDisposable = vscode.commands.registerCommand('vscode-grasshopper.disconnect', () => {
        client.disconnect();
    });
    context.subscriptions.push(connectDisposable);
    context.subscriptions.push(disconnectDisposable);
}
exports.activate = activate;
/**
 * 拡張機能の非アクティブ化時に呼び出される
 */
function deactivate() {
    if (client) {
        client.disconnect();
    }
}
exports.deactivate = deactivate;
//# sourceMappingURL=extension.js.map