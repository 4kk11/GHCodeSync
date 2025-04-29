import * as WebSocket from 'ws';
import { UIManager } from './managers/ui-manager';

/**
 * GrasshopperClientクラス
 * Grasshopperとの WebSocket 通信を管理する
 */
export class GrasshopperClient {
    private socket: WebSocket | null = null;
    private port: number = 51234;

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
            this.socket = new WebSocket(`ws://localhost:${this.port}`);
            
            this.socket.on('open', () => {
                this.uiManager.updateStatusBar('Connected', '$(check)');
                this.uiManager.showInfo('Connected to Grasshopper');
            });

            this.socket.on('message', (data: WebSocket.RawData) => {
                this.handleMessage(data);
            });

            this.socket.on('error', (error: Error) => {
                this.uiManager.showError(`Connection error: ${error.message}`);
                this.uiManager.updateStatusBar('Error', '$(error)');
            });

            this.socket.on('close', () => {
                this.uiManager.updateStatusBar('Disconnected', '$(circle-slash)');
                this.uiManager.showInfo('Disconnected from Grasshopper');
            });
        } catch (error) {
            this.uiManager.showError(`Connection error: ${error}`);
            this.uiManager.updateStatusBar('Error', '$(error)');
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
                    this.uiManager.showInfo('Script has been updated');
                    break;
                case 'error':
                    this.uiManager.showError(`Grasshopper error: ${message.message}`);
                    break;
                default:
                    console.log('Unknown message type:', message.type);
            }
        } catch (error) {
            console.error('Failed to parse message:', error);
        }
    }

    /**
     * スクリプトの更新をGrasshopper側に送信
     */
    async sendScriptUpdate(componentId: string, code: string): Promise<void> {
        if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
            throw new Error('Not connected to Grasshopper');
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

    /**
     * Grasshopperの健康状態を確認
     */
    healthCheck(): void {
        if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
            this.uiManager.showError('Not connected to Grasshopper');
            return;
        }

        const message = {
            type: 'healthCheck'
        };

        this.socket.send(JSON.stringify(message), (error) => {
            if (error) {
                this.uiManager.showError(`Health check failed: ${error}`);
            } else {
                this.uiManager.showInfo('Grasshopper health check completed');
            }
        });
    }
}