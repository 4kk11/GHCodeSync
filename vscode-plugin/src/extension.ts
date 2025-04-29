import * as vscode from 'vscode';
import { GrasshopperClient } from './grasshopper-client';
import { UIManager } from './managers/ui-manager';
import { EditorManager } from './managers/editor-manager';
import { FileSystemManager } from './managers/file-system-manager';

let client: GrasshopperClient;
let uiManager: UIManager;
let editorManager: EditorManager;
let fileSystemManager: FileSystemManager;

/**
 * 拡張機能のアクティベーション時に呼び出される
 */
export function activate(context: vscode.ExtensionContext) {
    // 各マネージャーの初期化
    uiManager = new UIManager();
    client = new GrasshopperClient(uiManager);
    editorManager = new EditorManager(client);
    fileSystemManager = new FileSystemManager(editorManager);

    // Grasshopperとの接続
    client.ensureConnection();

    // 監視の開始
    editorManager.startWatching(context);
    fileSystemManager.startWatching(context);

    uiManager.showInfo('Grasshopper extension is now active');

    // コマンドの登録
    let healthCheckDisposable = vscode.commands.registerCommand('GHCodeSync.healthCheck', () => {
        client.healthCheck();
    });

    let connectDisposable = vscode.commands.registerCommand('GHCodeSync.connect', () => {
        client.connect();
    });

    let disconnectDisposable = vscode.commands.registerCommand('GHCodeSync.disconnect', () => {
        client.disconnect();
    });

    context.subscriptions.push(healthCheckDisposable);
    context.subscriptions.push(connectDisposable);
    context.subscriptions.push(disconnectDisposable);
}

/**
 * 拡張機能の非アクティブ化時に呼び出される
 */
export function deactivate() {
    if (client) {
        client.disconnect();
    }
    if (fileSystemManager) {
        fileSystemManager.cleanup();
    }
}