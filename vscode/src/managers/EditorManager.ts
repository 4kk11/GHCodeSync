import * as vscode from 'vscode';
import { GrasshopperClient } from '../GrasshopperClient';
import * as path from 'path';

/**
 * エディタの監視と操作を管理するクラス
 */
export class EditorManager {
    private isSaving: boolean = false;

    constructor(
        private client: GrasshopperClient
    ) {}

    /**
     * エディタの監視を開始
     */
    startWatching(context: vscode.ExtensionContext): void {
        // アクティブエディタが変更された時の処理
        context.subscriptions.push(
            vscode.window.onDidChangeActiveTextEditor(() => {
                this.client.ensureConnection();
            })
        );

        // ドキュメントの保存時の処理
        context.subscriptions.push(
            vscode.workspace.onDidSaveTextDocument(document => {
                this.handleDocumentSave(document);
            })
        );
    }

    /**
     * ドキュメント保存時の処理
     */
    private async handleDocumentSave(document: vscode.TextDocument): Promise<void> {
        if (this.isSaving) return;
        this.isSaving = true;

        try {
            // C#ファイルの場合のみ処理
            if (document.languageId === 'csharp') {
                const fileName = path.basename(document.fileName);
                const guid = fileName.substring(0, fileName.lastIndexOf('.'));
                await this.client.sendScriptUpdate(guid, document.getText());
            }
        } catch (error) {
            vscode.window.showErrorMessage(`スクリプトの更新に失敗しました: ${error}`);
        } finally {
            this.isSaving = false;
        }
    }

    /**
     * 指定されたパスのドキュメントを開く
     */
    async openDocument(filePath: string): Promise<void> {
        try {
            const doc = await vscode.workspace.openTextDocument(vscode.Uri.file(filePath));
            await vscode.window.showTextDocument(doc, {
                preview: false,
                viewColumn: vscode.ViewColumn.Active
            });
        } catch (err) {
            console.error('Failed to open file:', err);
            throw err;
        }
    }
}