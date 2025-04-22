import * as vscode from 'vscode';
import { GrasshopperClient } from './GrasshopperClient';
import { UIManager } from './managers/UIManager';
import { EditorManager } from './managers/EditorManager';
import { FileSystemManager } from './managers/FileSystemManager';

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

    // 監視の開始
    editorManager.startWatching(context);
    fileSystemManager.startWatching(context);

    uiManager.showInfo('Grasshopper拡張機能がアクティブになりました');

    // コマンドの登録
    let connectDisposable = vscode.commands.registerCommand('vscode-grasshopper.connect', () => {
        client.connect();
    });

    let disconnectDisposable = vscode.commands.registerCommand('vscode-grasshopper.disconnect', () => {
        client.disconnect();
    });

    let healthCheckDisposable = vscode.commands.registerCommand('vscode-grasshopper.healthCheck', () => {
        uiManager.showInfo('Grasshopperの状態を確認しました!');
    });

    context.subscriptions.push(connectDisposable);
    context.subscriptions.push(disconnectDisposable);
    context.subscriptions.push(healthCheckDisposable);
}

/**
 * 拡張機能の非アクティブ化時に呼び出される
 */
export function deactivate() {
    if (client) {
        client.disconnect();
    }
}