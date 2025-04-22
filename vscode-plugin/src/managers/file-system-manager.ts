import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { EditorManager } from './editor-manager';

interface ConnectCmd {
    command: string;
    guid: string;
}

/**
 * ファイルシステムの監視を管理するクラス
 */
export class FileSystemManager {
    constructor(
        private editorManager: EditorManager
    ) {}

    /**
     * ファイルシステムの監視を開始
     */
    startWatching(context: vscode.ExtensionContext): void {
        const pattern = new vscode.RelativePattern(
            vscode.workspace.workspaceFolders![0],
            '**/connect.cmd'
        );
        const folderWatcher = vscode.workspace.createFileSystemWatcher(pattern, false, false, true);
    
        // FileSystemWatcherをsubscriptionsに追加
        context.subscriptions.push(folderWatcher);

        // onDidChangeのイベントリスナーをsubscriptionsに追加
        context.subscriptions.push(
            folderWatcher.onDidChange(async (uri) => {
                this.handleCmdChanged(uri.fsPath);
            })
        );
    }

    /**
     * connect.cmdファイルの変更時の処理
     */
    private async handleCmdChanged(cmdPath: string): Promise<void> {
        try {
            const connectCmd = this.getConnectCmd(cmdPath);
            if (!connectCmd) throw new Error('connect.cmd not found');

            // .cs ファイル
            const csPath = path.join(path.dirname(cmdPath), `${connectCmd.guid}.cs`);
            if (!fs.existsSync(csPath)) throw new Error(`C# file not found: ${csPath}`);

            await this.editorManager.openDocument(csPath);
        } catch (err) {
            console.error('Failed to handle cmd change:', err);
        }
    }

    /**
     * connect.cmdファイルからフィールドを取得するヘルパー関数
     */
    private getConnectCmd(filePath: string): ConnectCmd | null {
        if (!filePath) throw new Error('filePath is required');
        if (!fs.existsSync(filePath)) return null;

        const connectCmd = JSON.parse(fs.readFileSync(filePath, 'utf8'));

        if (!connectCmd) throw new Error('connect.cmd is empty or invalid');
        if (!connectCmd.command) throw new Error('command is required');
        if (!connectCmd.guid) throw new Error('guid is required');

        return connectCmd;
    }
}