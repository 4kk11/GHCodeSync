import * as WebSocket from 'ws';
import { UIManager } from './managers/ui-manager';

/**
 * GrasshopperClientクラス
 * Grasshopperとの WebSocket 通信を管理する
 */
export class GrasshopperClient {
    private socket: WebSocket | null = null;

    constructor(
        private uiManager: UIManager
    ) {}

    /**
     * WebSocket接続が確立されていない場合に接続を試みる
     */
    ensureConnection(): void {
        if (!this.socket) {
            this.connect();
        }
    }

    /**
     * Grasshopper（WebSocketサーバー）への接続を開始
     */
    connect(): void {
        try {
            this.socket = new WebSocket('ws://localhost:8080');
            
            this.socket.on('open', () => {
                this.uiManager.updateStatusBar('接続済み', '$(check)');
                this.uiManager.showInfo('Grasshopperに接続しました');
            });

            this.socket.on('message', (data: WebSocket.RawData) => {
                this.handleMessage(data);
            });

            this.socket.on('error', (error: Error) => {
                this.uiManager.showError(`接続エラー: ${error.message}`);
                this.uiManager.updateStatusBar('エラー', '$(error)');
            });

            this.socket.on('close', () => {
                this.uiManager.updateStatusBar('未接続', '$(circle-slash)');
                this.uiManager.showInfo('Grasshopperから切断されました');
            });
        } catch (error) {
            this.uiManager.showError(`接続エラー: ${error}`);
            this.uiManager.updateStatusBar('エラー', '$(error)');
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
     * Grasshopperからのメッセージを処理
     */
    private handleMessage(data: WebSocket.RawData): void {
        try {
            const message = JSON.parse(data.toString());
            switch (message.type) {
                case 'scriptUpdated':
                    this.uiManager.showInfo('スクリプトが更新されました');
                    break;
                case 'error':
                    this.uiManager.showError(`Grasshopperエラー: ${message.message}`);
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