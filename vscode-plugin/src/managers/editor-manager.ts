import * as vscode from 'vscode';
import { GrasshopperClient } from '../grasshopper-client';
import * as path from 'path';
import * as fs from 'fs';

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
                
                // .csprojファイルを更新
                await this.updateCsprojWithNugetReferences(document);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Failed to update script: ${error}`);
        } finally {
            this.isSaving = false;
        }
    }

    /**
     * コメントアウトされた#rディレクティブから.csprojを更新
     */
    private async updateCsprojWithNugetReferences(document: vscode.TextDocument): Promise<void> {
        const content = document.getText();
        const csprojPath = path.join(path.dirname(document.fileName), 'gh_component.csproj');
        
        if (!fs.existsSync(csprojPath)) {
            return; // .csprojファイルがない場合は何もしない
        }

        // コメントアウトされた#rディレクティブを抽出
        const nugetPattern = /\/\/\s*#r\s+"nuget:\s*([^,"\s]+)(?:\s*,\s*([^"]+))?"/g;
        const matches = [...content.matchAll(nugetPattern)];
        
        if (matches.length === 0) {
            return; // #rディレクティブがない場合は何もしない
        }

        // .csprojファイルを読み込み
        let csprojContent = fs.readFileSync(csprojPath, 'utf8');
        
        // 各パッケージを処理
        for (const match of matches) {
            const packageName = match[1].trim();
            const packageVersion = match[2] ? match[2].trim() : '*';
            
            // 既存のパッケージ参照を検索
            const existingPackagePattern = new RegExp(
                `<PackageReference\\s+Include="${packageName}"\\s+Version="([^"]+)"\\s*/>`,
                'i'
            );
            const existingMatch = csprojContent.match(existingPackagePattern);
            
            if (existingMatch) {
                // パッケージが存在する場合
                const existingVersion = existingMatch[1];
                if (existingVersion !== packageVersion) {
                    // バージョンが異なる場合は更新
                    csprojContent = csprojContent.replace(
                        existingPackagePattern,
                        `<PackageReference Include="${packageName}" Version="${packageVersion}" />`
                    );
                }
                // バージョンが同じ場合は何もしない
            } else {
                // パッケージが存在しない場合は追加
                const packageRef = `    <PackageReference Include="${packageName}" Version="${packageVersion}" />`;
                // 最初の</ItemGroup>の前に挿入
                const firstItemGroupIndex = csprojContent.indexOf('</ItemGroup>');
                if (firstItemGroupIndex !== -1) {
                    csprojContent = csprojContent.slice(0, firstItemGroupIndex) + 
                                   packageRef + '\n' + 
                                   csprojContent.slice(firstItemGroupIndex);
                }
            }
        }
        
        // .csprojファイルを保存
        fs.writeFileSync(csprojPath, csprojContent, 'utf8');
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