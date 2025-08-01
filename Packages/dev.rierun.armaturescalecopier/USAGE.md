# Armature Scale Copier - 使用方法詳細

## 概要

Armature Scale Copier は VRChat 向けの Unity Editor ツールで、Armature オブジェクトの子にある MA Scale adjust などのコンポーネントや Transform 情報を別の Armature オブジェクトにコピー・ペーストできます。

## インストール方法

### UnityPackage インストール

1. Unity を開く
2. ダウンロードした UnityPackage を選択
3. 「Import」をクリック

## 主要機能

### 1. Single Copier（メインツール）

**場所**: `Tools → ArmatureScaleCopier → Single Copier`

#### 基本的な使用方法

1. ツールウィンドウを開く
2. **コピー元**: 元となる Armature オブジェクトを選択
3. **Copy Armature Data**: ボタンをクリックしてデータをコピー
4. **コピー先**: ペースト先の Armature オブジェクトを選択
5. **Paste Armature Data**: ボタンをクリックしてデータをペースト

#### オプション

-   **Transform 情報をコピー**: 位置、回転、スケール情報をコピー
-   **ModularAvatar コンポーネントをコピー**: MA Scale adjust、MA Merge Armature などをコピー
-   **その他のコンポーネントをコピー**: Unity 標準コンポーネントなどもコピー

### 2. Avatar Batch Copier（一括操作）

**場所**: `Tools → ArmatureScaleCopier → Avatar Batch Copier`

#### 一括コピー

1. **コピー元 Armature**: 元となる Armature オブジェクトを選択
2. Armature 一覧でコピー先をチェックボックスで複数選択
3. **選択された Armature にコピー**: ボタンで一括実行

## 使用例

### 例 1: 複数の衣装に同じ調整を適用

```
1. Avatar Batch Copierを開く
2. アバターを検索範囲にD&D
3. 複数の対象Armatureをチェックで選択
4. 一括コピーを実行
```

### 例 2: 一つだけの衣装のスケールをコピー

```
1. コピー元のArmatureオブジェクトを選択
2. Single Copierを開く
3. Copy Armature Dataでデータをコピー
4. 新しい衣装のArmatureオブジェクトを選択
5. Paste Armature Dataで調整をコピー
```

## 対応コンポーネント

### ModularAvatar コンポーネント

### その他

-   Transform（位置、回転、スケール）
-   カスタムコンポーネント（オプション）

## トラブルシューティング

### よくある問題

#### Q: ModularAvatar コンポーネントがコピーされない

A: ModularAvatar 1.8.0 以上がプロジェクトにインストールされていることを確認してください。

#### Q: コピー後にコンポーネントが正常に動作しない

A: 一部のコンポーネントは参照が必要な場合があります。手動で設定し直してください。

### エラーメッセージ

-   **"選択されたオブジェクトは「Armature」という名前ではありません"**: オブジェクト名に"Armature"が含まれていることを確認
-   **"コンポーネント XXX の適用に失敗しました"**: 対象のコンポーネントが互換性がない可能性があります

## 制限事項

-   プレハブモードでは使用できません
-   一部のコンポーネントは完全にコピーできない場合があります
-   複雑な参照関係がある場合は手動調整が必要な場合があります

## 免責事項

本ツールを使用することで発生したいかなる問題についても、開発者は責任を負いません。自己責任でご利用ください。  
事前にバックアップやアバターのコピーなどを行うことを推奨します。

## サポート

問題や要望がある場合は、Booth のショップまたは、X（旧 Twitter）でお問い合わせください。
