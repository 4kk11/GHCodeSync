import * as vscode from 'vscode';

/**
 * VSCodeのUI要素を管理するクラス
 */
export class UIManager {
    private statusBarItem: vscode.StatusBarItem;

    constructor() {
        this.statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right);
        this.updateStatusBar('未接続', '$(circle-slash)');
    }

    /**
     * ステータスバーの表示を更新
     */
    updateStatusBar(text: string, icon: string): void {
        this.statusBarItem.text = `${icon} Grasshopper: ${text}`;
        this.statusBarItem.show();
    }

    /**
     * 情報メッセージを表示
     */
    showInfo(message: string): void {
        vscode.window.showInformationMessage(message);
    }

    /**
     * エラーメッセージを表示
     */
    showError(message: string): void {
        vscode.window.showErrorMessage(message);
    }
}