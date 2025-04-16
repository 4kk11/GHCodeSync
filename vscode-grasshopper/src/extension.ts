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
    /** WebSocket接続インスタンス */
    private socket: WebSocket | null = null;
    /** VSCodeのステータスバーアイテム */
    private statusBarItem: vscode.StatusBarItem;

    constructor() {
        // ステータスバーアイテムを初期化
        this.statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right);
        this.updateStatusBar('未接続', '$(circle-slash)');
    }

    /**
     * Grasshopper（WebSocketサーバー）への接続を開始
     * - 接続イベントのハンドラを設定
     * - エラー発生時の処理を定義
     */
    connect(): void {
        try {
            // WebSocketサーバーへの接続を開始
            this.socket = new WebSocket('ws://localhost:8080');
            
            // 接続成功時の処理
            this.socket.on('open', () => {
                this.updateStatusBar('接続済み', '$(check)');
                vscode.window.showInformationMessage('Grasshopperに接続しました');
            });

            // メッセージ受信時の処理
            this.socket.on('message', (data: WebSocket.RawData) => {
                const message = JSON.parse(data.toString());
                this.handleMessage(message);
            });

            // エラー発生時の処理
            this.socket.on('error', (error: Error) => {
                vscode.window.showErrorMessage(`接続エラー: ${error.message}`);
                this.updateStatusBar('エラー', '$(error)');
            });

            // 接続切断時の処理
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
     * @param text 表示するステータステキスト
     * @param icon 表示するアイコン（VSCode icon ID）
     */
    private updateStatusBar(text: string, icon: string): void {
        this.statusBarItem.text = `${icon} Grasshopper: ${text}`;
        this.statusBarItem.show();
    }

    /**
     * Grasshopperからのメッセージを処理
     * メッセージタイプに応じて適切な処理を実行
     * @param message 受信したメッセージオブジェクト
     */
    private handleMessage(message: any): void {
        switch (message.type) {
            case 'scriptUpdated':
                // Grasshopper側からスクリプトが更新された場合の処理
                // TODO: VSCodeエディタの内容を更新する処理を実装
                break;
            default:
                console.log('未知のメッセージタイプ:', message.type);
        }
    }

    /**
     * スクリプトの更新をGrasshopper側に送信
     * @param componentId 更新対象のコンポーネントID
     * @param code 更新するスクリプトのコード
     */
    sendScriptUpdate(componentId: string, code: string): void {
        // 接続状態のチェック
        if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
            vscode.window.showErrorMessage('Grasshopperに接続されていません');
            return;
        }

        // 更新メッセージの構築
        const message = {
            type: 'setScript',
            target: componentId,
            code: code,
            language: 'csharp'
        };

        // メッセージの送信
        this.socket.send(JSON.stringify(message));
    }
}

/** グローバルなクライアントインスタンス */
let client: GrasshopperClient;

/**
 * 拡張機能のアクティベーション時に呼び出される
 * - クライアントの初期化
 * - コマンドの登録
 * @param context 拡張機能のコンテキスト
 */
export function activate(context: vscode.ExtensionContext) {
    // クライアントの初期化
    client = new GrasshopperClient();

    // Grasshopperへの接続コマンドを登録
    let connectDisposable = vscode.commands.registerCommand('vscode-grasshopper.connect', () => {
        client.connect();
    });

    // Grasshopperからの切断コマンドを登録
    let disconnectDisposable = vscode.commands.registerCommand('vscode-grasshopper.disconnect', () => {
        client.disconnect();
    });

    // コマンドの登録解除用にサブスクリプションに追加
    context.subscriptions.push(connectDisposable);
    context.subscriptions.push(disconnectDisposable);
}

/**
 * 拡張機能の非アクティブ化時に呼び出される
 * - Grasshopperとの接続を切断
 */
export function deactivate() {
    if (client) {
        client.disconnect();
    }
}