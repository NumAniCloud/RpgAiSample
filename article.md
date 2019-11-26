# RPGのAIを総当たりで実装する

この記事は C# Advent Calendar 2019 の1日目です。

私はRPGを作るのが大好きです。特に、ちょっと複雑な効果を持ったユニークなスキルを実装するのが好きです。しかし、そういったスキルを敵に使わせるとなると話は別です。

「攻撃力の高いスキル」と「自分のHPを回復するスキル」があったとして、どちらを使うと良いでしょうか？それは場合によります。1撃で敵を倒せるならば前者ですし、逆に自分が1撃で倒れそうなら後者かもしれません。

実際にゲームシステムを組みながら、AIをどのような発想で実装するとよいか、その際にプログラムの設計で気を付けることなどを紹介します。

## ゲームシステム

今回実装するゲームは以下のようなシステムです：

* プレイヤー1体と敵1体がいて、お互いに相手のHPが0になることを目指す
* プレイヤーはAIによって自動で行動する。
* 敵は固定の行動しかとらない。
* プレイヤーは複数のスキルから何らかの手段でスキルを選んで使用できる。
* スキルには攻撃力が、戦闘参加者には防御力がある。
    * (ダメージ) = (攻撃力) - (防御力)
* 決着がつくとプログラムは終了する。
* 戦闘の様子はコンソール ウィンドウに出力される。

ユーザーが操作できる部分すらなく寂しい感じですが、今回はAIを実装したいだけなのでバッサリ割愛しました。

## 下準備

まずはゲームの全体の流れを作成します。
つまり、バトルの参加者に関する情報の初期化や、
ゲームの勝利条件の判定などのことです。

まだ定義していないクラスが多数登場しますので、この後ひとつづつ実装していきます。

```csharp:Program.cs
class Program
{
    public static void Main()
    {
        // HPが低いが防御が高い敵と、HPが高く防御が低い敵を作成
        // BattleContext.Enemy にどちらを渡すかによって、対戦相手を差し替えることができる
        var enemy1 = new EnemyBattler()
        {
            Hp = 45,
            Defense = 25,
        };
        var enemy2 = new EnemyBattler()
        {
            Hp = 100,
            Defense = 0,
        };

        var context = new BattleContext()
        {
            Enemy = enemy1,
            Player = new PlayerBattler()
            {
                Hp = 100,
                Defense = 0,
                Skills = new Skill[]
                {
                    new SingleAttackSkill(87),  // ここでスキルの攻撃力を設定
                    new TripleAttackSkill(39),  // ここでスキルの攻撃力を設定
                }
            }
        };

        Console.WriteLine($"プレイヤーのHP:{context.Player.Hp}");
        Console.WriteLine($"敵のHP:{context.Enemy.Hp}");

        while(true)
        {
            context.Player.Act(context);
            if(context.Enemy.Hp <= 0)
            {
                Console.WriteLine("敵は倒れた！");
                Console.WriteLine("プレイヤーの勝ち");
                return;
            }

            context.Enemy.Act(context);
            if(context.Player.Hp <= 0)
            {
                Console.WriteLine("プレイヤーは倒れた！");
                Console.WriteLine("敵の勝ち");
                return;
            }
        }
    }
}
```

次に定義するのは、バトル全体にわたって必要になる機能をまとめる `BattleContext` クラスです。

```csharp:BattleContext.cs
class BattleContext
{
    public EnemyBattler Enemy { get; set; }
    public PlayerBattler Player { get; set; }
}
```

独特で多様なスキルをたくさん作るためにも、戦況に関わる情報はなるべくどこからでも書き換えできるように、 `BattleContext` のプロパティに押し込めて様々なクラスに受け渡します。敵とプレイヤーの情報が同時に必要になる場面はいくらかあるので、こうして固めておいて、メソッドの引数の定義が簡潔になることを狙っています（パラメータ オブジェクトといいます）。

可能な限りスコープを小さく書き換え不能にするというオブジェクト指向の原則からは外れてしまっているかもしれません。

次は、バトルの主役である `EnemyBattler`, `PlayerBattler` を定義します。まずはそれらの基底クラスとして、敵にもプレイヤーにもあるHPと防御力を持たせた `Battler` クラスを定義します。

```csharp:Battler.cs
class Battler
{
    public int Hp { get; set; }
    public int Defense { get; set; }
}
```

そして、 `EnemyBattler` クラスを定義します。このクラスは、ターンが回ってきたときの行動を実行する `Act` メソッドを持ちます。敵のAIとして、プレイヤーに対して119の固定ダメージを及ぼす攻撃をさせることにします。

```csharp:EnemyBattler.cs
class EnemyBattler : Battler
{
    public void Act(BattleContext context)
    {
        Console.WriteLine("敵の攻撃");
        Console.WriteLine("プレイヤーに 119 のダメージ");
        context.Player.Hp -= 119;
    }
}
```

つぎに `PlayerBattler` クラスを定義します。このクラスも敵と同じ役割である `Act` メソッドを持ちますが、行動内容としてスキルを適当に選び、実行します。今回は、持っているスキルから先頭のものを必ず使うようにしましょう。

```csharp:PlayerBattler.cs
class PlayerBattler : Battler
{
    public Skill[] Skills { get; set; }

    public void Act(BattleContext context)
    {
        Skills[0].Run(context, context.Enemy);
    }
}
```

詳しくは前述の `Program.cs` に書かれていますが、スキルの配列には次のものを決め打ちで渡します：

* 0番目は1回攻撃のスキルで、威力87
* 1番目は3回攻撃のスキルで、威力39

スキルとは、次のようなクラスです。

```csharp:Skill.cs
abstract class Skill
{
    public abstract void Run(BattleContext context, Battler target);
}
```

`Skill` には、 `SingleAttackSkill`, `TripleAttackSkill` という2つのバリエーションがあります。 `SingleAttackSkill` は、敵に一回だけ攻撃するスキルです。

```csharp:SingleAttackSkill.cs
sealed class SingleAttackSkill : Skill
{
    public int Power { get; private set; }

    public SingleAttackSkill(int power)
    {
        Power = power;
    }

    public override void Run(BattleContext context, Battler target)
    {
        Console.WriteLine("あなたは狙いを定めて敵を撃ちぬいた！");

        var damage = Power - target.Defense;    // ダメージ計算
        target.Hp -= damage;                    // 実際にHPを減らす
        Console.WriteLine($"敵に{damage}のダメージ！");
    }
}
```

`TripleAttackSkill` は、敵に3回連続で攻撃するスキルです。

```csharp:TripleAttackSkill.cs
sealed class TripleAttackSkill : Skill
{
    public int Power { get; private set; }

    public TripleAttackSkill(int power)
    {
        Power = power;
    }

    public override void Run(BattleContext context, Battler target)
    {
        Console.WriteLine("あなたは敵の体へ銃を3連射した！");

        var singleDamage = Power - target.Defense;  // ダメージ計算
        target.Hp -= singleDamage * 3;              // 実際にHPを減らす
        Console.WriteLine($"敵に {singleDamage} のダメージ！");
        Console.WriteLine($"敵に {singleDamage} のダメージ！");
        Console.WriteLine($"敵に {singleDamage} のダメージ！");
    }
}
```

## 実行

上記のサンプルでは、 `BattleContext.Enemy` プロパティに `enemy1` 変数の内容を設定してあります。このまま実行すると次のようになります：

```
プレイヤーのHP:100
敵のHP:45
あなたは狙いを定めて敵を撃ちぬいた！
敵に62のダメージ！
敵は倒れた！
プレイヤーの勝ち
```

用意した1回攻撃のスキルは威力が`87`で、敵の防御力によって`25`軽減されましたが、それでも敵のHP`45`を超えるダメージを与えて倒すことができました。

`BattleContext.Enemy` プロパティに `enemy2` 変数の内容を代入するように書き換えてみてください。それを実行すると次のようになります：

```
プレイヤーのHP:100
敵のHP:100
あなたは狙いを定めて敵を撃ちぬいた！
敵に87のダメージ！
敵の攻撃
プレイヤーに 119 のダメージ
プレイヤーは倒れた！
敵の勝ち
```

用意した1回攻撃のスキルは威力が`87`で、敵の防御力は`0`なのでダメージは減りませんでしたが、それでも敵のHP`100`を超えるダメージを与えられなかったので倒しきれず、反撃でやられてしまいました。

そこで、`PlayerBattler` の選択するスキルを0番目のスキルではなく1番目のスキルに変えてみるとどうでしょうか。書き換える場所は、 `PlayerBattler.cs` の `Act` メソッドの中です。1番目のスキルには「3回攻撃」が割り当てられているはずです。これで実行してみましょう。

```
プレイヤーのHP:100
敵のHP:100
あなたは敵の体へ銃を3連射した！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵は倒れた！
プレイヤーの勝ち
```

敵に`39*3`のダメージを与え、これはHP`100`を超えているので倒すことができました。

しかし、いつも三回攻撃のスキルを選べば良いわけではありませんよね。対戦相手を `enemy1` に戻すと次のような結果になります：

```
プレイヤーのHP:100
敵のHP:45
あなたは敵の体へ銃を3連射した！
敵に 14 のダメージ！
敵に 14 のダメージ！
敵に 14 のダメージ！
敵の攻撃
プレイヤーに 119 のダメージ
プレイヤーは倒れた！
敵の勝ち
```

3回攻撃スキルは1回のダメージが`39`ですが、敵の防御力`15`により軽減され、ダメージは`14*3=42`しか与えられませんでした。これだとHP`45`を削り切れないので、反撃でやられてしまいました。

こうなるようにルールを作ったので、プレイヤーは適切なスキルを考えて選択する必要があるわけです。

## AIにやらせる

でも、このように適切なスキルを選ばなければならないのは敵も同じです。敵キャラクターの行動はプレイヤーに選択させるわけにはいかないため、AIでスキルを決定する必要があるはずです。さて、AIに適切なスキルを選ばせるためにはどうすればよいのでしょうか？

攻撃力が高いスキルを選ぶのがよいでしょうか？でも、攻撃回数が多くて攻撃力の低いスキルの方が強いかもしれません。もしかしたら自分に攻撃力アップの状態変化がついているかもしれませんし、ほかにも、このターンは攻撃せずに敵に毒状態などを与えた方がいいのかもしれません。どんなスキルもシンプルな考え方で評価できる方法はないでしょうか？

今回紹介するのは、すべてのスキルに対して、それを使った結果をシミュレーションし、攻撃結果だけを評価する方法です。サンプルプログラムでは、敵側ではなくプレイヤーキャラクターが自動で適切なスキルを選ぶことができるAIを作ってみましょう（それはプレイヤーキャラクターとは言わない気がしますが悪しからず）。

## スキルをシミュレーションするAI

今回紹介する方法では、プレイヤーのAIは次のように実装します。入力として、選択肢となるスキルの配列を渡し、結果としてそのスキルの配列の中で最も効果的なものを選んで返します。詳細はこの後すぐ説明します。

```csharp:PlayerAi.cs
class PlayerAi
{
    public Skill DetermineSkill(BattleContext context, Skill[] skills)
    {
        (Skill, int priority) candidate = (null, -context.Enemy.Hp);

        foreach (var skill in skills)
        {
            // A. シミュレーション中に敵が受けるダメージを実際には反映しないためのクローン
            var clone = new EnemyBattler()
            {
                Hp = context.Enemy.Hp,
                Defense = context.Enemy.Defense
            };

            // B. スキルを実際に適用してみる
            skill.Run(context, clone);

            // C. スキルの仕様結果を評価する。
            // 敵のHPが少ないほど好ましい状況のはず
            var priority = -clone.Hp;
            if (candidate.priority < priority)
            {
                candidate = (skill, priority);
            }
        }

        return candidate.Item1;
    }
}
```

このコードについて詳しく見てみましょう。

### B. 本当にスキルを適用しているだけ

コメント `B.` のところを見ると、本当にスキルを実行して試していることが分かりますね。

### A. スキルは敵のコピーに対して使用する

ただし、スキルの対象者として本物の `Enemy` を渡すわけにはいきません。そうしてしまうと、使うべきスキルが確定するころには敵キャラクターは全種類のスキルを喰らった後の満身創痍の状態になってしまい、それではゲームになりません。ですので、元の `EnemyBattler` のパラメータをコピーした新しい `EnemyBattler` を作成します。この2つは完全に別のオブジェクトですので、コピーの方のHPが書き換わっても元のオブジェクトのHPは書き換わりません。このようなコピーを作ることを「クローンする」といいます。

「クローン」と「クローンでないもの」の違いは以下のような感じです：

```csharp
// 元のオブジェクト。
var source = new EnemyBattler() { Hp = 100 };

// 変数 source を変数 notClone に代入しただけ。クローンじゃない。
// この2つの変数は参照先が同じ
var notClone = source;

// notClone.Hp を書き換えると source.Hp も書き換わってしまう。
notClone.Hp = 99;

// 変数 source のメンバー変数の値だけを引き継ぐ新しいオブジェクト。これがクローン。
// この2つの変数は参照先が違う
var clone = new EnemyBattler() { Hp = source.Hp };

// clone.Hp を書き換えても、 source.Hp は書き換わらない。
clone.Hp = 50;
```

### C. スキルを適用した結果を評価する

スキルを適用したら、実際にどれだけ有効だったかを評価します。最も評価が高かったスキルをAIが実際に使うように制御するわけです。

スキルがどれだけ有効だったか、その評価基準はゲームのルールに依存します。多くのRPGは相手のHPを最も良く削るものを選ぶでしょうし、ひょっとすると、プレイヤーのお金を盗むことが最優先事項である敵キャラなどもいるかもしれません。

今回は、敵のHPを最も削ることができるスキルを選ぶことにしましょう。`B.` でスキルを適用したので、変数 `clone` の表す敵キャラクターはHPが減っているはずです。そこで、HPの正負を逆転したものをそのまま、そのスキルの優先度としましょう（HPが大きいほど、優先度が下がりますからね）。そして、優先度が最も高いスキルを最後に選ぶのです。

変数 `canndidate` に、最も高かった優先度とその時のスキルを記録しておき、最後に残ったスキルが最も優先度の高いスキルとなりますので、それがAIの計算結果となります。

## AIを呼び出す

`PlayerBattler` クラスを以下のように書き換えましょう。

```csharp:PlayerBattler.cs
class PlayerBattler : Battler
{
    public Skill[] Skills { get; set; }

    private readonly PlayerAi ai;
    
    public PlayerBattler()
    {
        ai = new PlayerAi();
    }

    public void Act(BattleContext context)
    {
        var skill = ai.DetermineSkill(context, Skills);
        skill.Run(context, context.Enemy);
    }
}
```

`PlayerBattler` は `PlayerAi` を持ち、使うスキルを決定したいときはこのクラスに依頼します。スキルを発動する部分は今まで通りで、前もって決まったスキルを選ぶのではなくAIから返ってきたスキルを呼び出す、という点が今までと異なります。

## 新しいAIを実行

新しいAIを搭載した `PlayerBattler` を戦わせてみましょう。対戦相手を `enemy1` にして実行してみます。

```
レイヤーのHP:100
敵のHP:45
あなたは狙いを定めて敵を撃ちぬいた！
敵に62のダメージ！
あなたは敵の体へ銃を3連射した！
敵に 14 のダメージ！
敵に 14 のダメージ！
敵に 14 のダメージ！
あなたは狙いを定めて敵を撃ちぬいた！
敵に62のダメージ！
敵は倒れた！
プレイヤーの勝ち
```

……何かがおかしい気がしますが、最終的にはAIが「1回攻撃」を選択し、敵を倒すことができました。次は対戦相手を `enemy2` にしてみましょう。

```
プレイヤーのHP:100
敵のHP:100
あなたは狙いを定めて敵を撃ちぬいた！
敵に87のダメージ！
あなたは敵の体へ銃を3連射した！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵に 39 のダメージ！
あなたは敵の体へ銃を3連射した！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵は倒れた！
プレイヤーの勝ち
```

今度は敵の防御力に合わせて「3回攻撃」を選びました。確かに、戦況が最も良くなるスキルを選ぶことができているようです。余裕があれば、新しい対戦相手を追加してみると面白いです。HPが高すぎて倒しきれない相手であっても、可能な限りHPをたくさん削れるスキルを選ぶはずです。

## AIの実行中にメッセージが表示されてしまう

もうお気づきかもしれませんが、ここまでの実装だと、スキルのシミュレーション中にメッセージが表示されてしまいます。先ほどの例では、良く見ると1回行動するためにスキル3回ぶんのメッセージが表示されてしまっているのが分かると思います。全てのスキルを試しているので、スキルがN個あればメッセージはスキル(N+1)回ぶん表示されてしまうわけです。

この問題を回避するためには、メッセージの表示先を切り替えられるようにする必要があります。そして、「コンソールに表示する」モードと、「どこにも表示しない」モードを用意したいところです。今回の例だと表示先がコンソールでしたが、美麗なグラフィックのコンシューマーゲームだったとしても、スキルのキラキラしたエフェクトがシミュレーション中に全種類再生されたらカッコ悪いですから、やはり「どこにも表示しない」モードは必要になります。

表示先を切り替える機能は、インターフェースを用いたテクニックによってシンプルに実装できます。

## メッセージの表示先を差し替えられるようにする

### フラグを使った方法はどうか？

今のところ、メッセージを画面に表示するためには `Console.WriteLine` メソッドを使っていますね。

```csharp
Console.WriteLine("あなたは狙いを定めて敵を撃ちぬいた！");

var damage = Power - target.Defense;
target.Hp -= damage;
Console.WriteLine($"敵に{damage}のダメージ！");
```

これから実装したい「モード切り替え」機能はどのように実装するとよいでしょうか？試しに、`bool`型のフラグを1つ用意して、`true`のときはコンソールに表示し、`false`のときはどこにも表示しない、と決めたとするとどうなるでしょうか？そのフラグ `IsShown` は、バトル中のどこからでもアクセスできるつもりのオブジェクト `BattleContext` に持たせるとよいでしょう。すると、メッセージを表示する部分は以下のようになります:

```csharp
if (context.IsShown) Console.WriteLine("あなたは狙いを定めて敵を撃ちぬいた！");

var damage = Power - target.Defense;
target.Hp -= damage;
if (context.IsShown) Console.WriteLine($"敵に{damage}のダメージ！");
```

`Console.WriteLine` を呼び出すかどうかを、 `BattleContext.IsShown` フラグの状態によって分岐しています。しかしこの方法だと、 `Console.WriteLine` を呼び出す部分全てでif文を追加しなければなりません。これをゲームの完成までずっと、必ず忘れずに続けるのはなかなかに苦痛です。

### メソッドを使った方法

先ほどのフラグを使った方法では、if文で分岐をするという処理が繰り返し登場していました。繰り返し登場する処理はメソッドによって共通化するというのはよい考えです。そのメソッドを `BattleContext` クラスに足してみるとどうなるでしょう。そのメソッドは以下のようなものです：

```csharp:BattleContext.cs
// 前略
    public void Talk(string message)
    {
        // isShownというprivateフィールドをBattleContextに追加しておく。
        if(isShown) Console.WriteLine(message);
    }
// 後略
```

呼び出し側は以下のようになります：

```csharp
context.Talk("あなたは狙いを定めて敵を撃ちぬいた！");

var damage = Power - target.Defense;
target.Hp -= damage;
context.Talk($"敵に{damage}のダメージ！");
```

なかなかすっきりした記述になりましたね。これなら面倒がらずに書くことができそうです。

しかしこの書き方にも問題はあります。 `BattleContext` は元々、バトルの制御に必要な情報をまとめるのが責務であり、そのためのプロパティが用意されています。そこにこういった実際に何らかの処理を行うメソッドが追加された場合、そのメソッドが元々あったプロパティに不正な値を代入したりしないよう気を付けなければなりません。

今回は単純なメソッドなのでよいかもしれませんが、今後もずっとそうとは限りません。ゲーム開発はどんな仕様が正解なのかがはじめからは定まっていませんから、仕様変更により `BattleContext` の実装の信頼性が少しづつ不安定になっていくかもしれません。

### インターフェースを使った方法

インターフェースを使って、メッセージの表示先を `Skill` 側が意識しなくて済むようにしてみましょう。さしあたっての目標は、以下のようなメソッド呼び出しを：

```csharp
Console.WriteLine("あなたは狙いを定めて敵を撃ちぬいた！");
```

以下のように書き換え、メッセージ データがどのような機能へ流れ着くのかを隠蔽します。

```csharp
// Talkメソッド自体は表示の作業はせず、あくまでどのような機能へデータを流すかを制御するだけ
context.View.Talk("あなたは狙いを定めて敵を撃ちぬいた！");
```

そのようなインターフェースとして、以下のようなものを定義します。これが「メッセージを表示する機能」を表すインターフェースとなります。

```csharp:IView.cs
interface IView
{
    void Talk(string text);
}
```

その実装……つまり「特定の方法でメッセージを表示するクラス」は、「コンソールに表示する」モードと「どこにも表示しない」モードの2つのためのクラスが必要です。

```csharp:ConsoleView.cs
// コンソールに表示するモード
class ConsoleView : IView
{
    public void Talk(string text)
    {
        Console.WriteLine(text);
    }
}
```

```csharp:NullView.cs
// どこにも表示しないモード
class NullView : IView
{
    public void Talk(string text)
    {
        // 何もしない
    }
}
```

`IView` インターフェースを実装するオブジェクトは、 `BattleContext` クラスに持たせることで、バトルの制御コード内のどこからでもアクセスできるようにしましょう。 `BattleContext` はあくまで情報をまとめる以外の責任は持たず、何か管轄外の要求が来た場合は `View` プロパティに設定されたオブジェクトに丸投げするつもりです。

```csharp:BattleContext.cs
class BattleContext
{
    public EnemyBattler Enemy { get; set; }
    public PlayerBattler Player { get; set; }
    public IView View { get; private set; }

    // コンストラクター引数から受け取って、読み取り専用プロパティに設定する
    // View プロパティの内容を後から書き換えることのない設計にするつもりのため
    public BattleContext(IView view)
    {
        View = view;
    }
}
```

そして、 `Program.cs` で `BattleContext` を生成している部分を書き換えます。`IView` を実装するオブジェクトとして、 `ConsoleView` を生成して渡してあげます。

```csharp:Program.cs(変更前)
// 前略
var context = new BattleContext()
{
    Enemy = enemy1,
    Player = new PlayerBattler()
    {
        Hp = 100,
        Defense = 0,
        Skills = new Skill[]
        {
            new SingleAttackSkill(87),
            new TripleAttackSkill(39),
        }
    }
};
// 後略
```

```csharp:Program.cs(変更後)
// 前略
var context = new BattleContext(new ConsoleView())
{
    Enemy = enemy2,
    Player = new PlayerBattler()
    {
        Hp = 100,
        Defense = 0,
        Skills = new Skill[]
        {
            new SingleAttackSkill(87),
            new TripleAttackSkill(39),
        }
    }
};
// 後略
```

この後は、`Console.WriteLine` を呼び出している部分を `context.View.Talk` に置き換えていく作業となります。スキルの発動に関係ない部分でも全て置き換えておくことをお勧めしますし、今回は全て置き換えた場合で説明します。

なかなか大変な作業ですし、実際の開発ではこういう仕様変更が起きる可能性を考えて前もってインターフェースを用いて差し替えられるようにしておくと良いかもしれません。そうすると良いのは、今回必要になったモードの他にも「iPhoneで動かすためのモード」「ゲームエンジンを用いてグラフィカルに表示するモード」などの様々な新しい要求が起きても対応できることです。

さて、ここまでの作業だと、動作は何も変わらないはずです。実行してみましょう：

```
プレイヤーのHP:100
敵のHP:100
あなたは狙いを定めて敵を撃ちぬいた！
敵に87のダメージ！
あなたは敵の体へ銃を3連射した！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵に 39 のダメージ！
あなたは敵の体へ銃を3連射した！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵は倒れた！
プレイヤーの勝ち
```

次に、`BattleContext`を生成するときに `NullView` を渡すようにしてみましょう。

```csharp:Program.cs
// 前略
var context = new BattleContext(new NullView())
{
    Enemy = enemy2,
    Player = new PlayerBattler()
    {
        Hp = 100,
        Defense = 0,
        Skills = new Skill[]
        {
            new SingleAttackSkill(87),
            new TripleAttackSkill(39),
        }
    }
};
// 後略
```

すると、今度は実行しても画面には何も表示しなくなるはずです。

これで下準備ができました。今度は、スキルのシミュレーション中は画面に何も表示せず、実際に発動するときにはちゃんと表示をするようにしたいところです。

この要求を満たすために修正した `PlayerAi` クラスは以下のようになります：

```csharp:PlayerAi.cs
class PlayerAi
{
    public Skill DetermineSkill(BattleContext context, Skill[] skills)
    {
        (Skill, int priority) candidate = (null, -context.Enemy.Hp);

        // *修正* シミュレーション中に発動するスキルのメッセージを表示しないようにするためのクローン
        var cloneContext = new BattleContext(new NullView())
        {
            Enemy = context.Enemy,
            Player = context.Player
        };

        foreach (var skill in skills)
        {
            // シミュレーション中に敵が受けるダメージを実際には反映しないためのクローン
            // 割愛しているが、実際はプレイヤーのクローンも生成しておいたり、
            // 敵のクローンはBattleContext.Enemyなどにもsetしておいたほうが
            // 独特なスキルをたくさん実装する際に安全
            var clone = new EnemyBattler()
            {
                Hp = context.Enemy.Hp,
                Defense = context.Enemy.Defense
            };

            // *修正* BattleContext を渡す場所には、メインの BattleContext ではなく
            // NullView を持たせてあるクローンのほうの BattleContext を渡す
            skill.Run(cloneContext, clone);

            var priority = -clone.Hp;
            if (candidate.priority < priority)
            {
                candidate = (skill, priority);
            }
        }

        return candidate.Item1;
    }
}
```

今回は、 `EnemyBattler` だけでなく `BattleContext` のクローンも作成しています。 `BattleContext.Enemy` プロパティと `BattleContext.Player` プロパティの中身は元々の `BattleContext` の中身を雑にコピーしていますが、これはクローンになっていないので、このプロパティを経由してHPを変更したりすると元々の `BattleContext` に影響が出てしまいます。実際には全てのメンバーについて、その子のメンバー、孫のメンバーというふうに再帰的に潜って完全に切り離されたクローンを作るべきです。

そして、シミュレーションのためにスキルを実行する際には、元々の `BattleContext` ではなく、 `NullView` を持たせてあるクローンの方を渡す必要があります。こうすることによって、スキルのシミュレーションをする時に限って画面への表示を禁止することができます。

さあ、この状態で実行してみましょう。対戦相手が `enemy2` ならば、次のようになるはずです。

```
プレイヤーのHP:100
敵のHP:100
あなたは敵の体へ銃を3連射した！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵に 39 のダメージ！
敵は倒れた！
プレイヤーの勝ち
```

きちんと適切なスキルを選べていますし、しかもシミュレーション中にスキルを実際に試していることはバレずに済んでいます！お疲れさまでした。

## まとめ

### 総当たりのAIも悪くない

RPGなどにおけるゲームAIを作る際、全てのパターンを試してみる、というのは悪くない方法です。いわゆる「総当たり」というやつです。この方法の問題点は最終的なスキルを決定するまでに時間がかかることですが、それが顕著になるパターンもいくつか考えられます：

* スキルの個数が数十個にも及ぶ場合
* スキルの処理が非常に複雑な場合
* 何回も連続で行動でき、スキルを使う順番によっても戦況が大きく変わる場合
    * スキルが2個で行動回数2回だったとしても、「使うか使わないか」「順番」によって6パターン試さなければなりません

そのような状態に陥った時には、私の場合は次に、行動パターンをランダムに打ち切る方法を使います。一部のスキルをランダムに、「評価する価値もなく不採用だ」と見なして切り捨てることで、シミュレーションの手間を省きます。時折、非常に強力なスキルを使うのを不意に諦めてしまって妙な感じになるかもしれませんが、強すぎるAIにするとゲームにならないですし、最強のスキルをひたすら撃つ敵ばかりになるとつまらないので、容認することにしています。

他にも色々な最適化方法があるかと思いますが、総当たりの手法を改善して効率的にしたものを使う、という発想はやはり有効と考えています。

### インターフェースを使おう

特定の処理を後で差し替えられるようにしたいとき、インターフェースを用いるのはよい方法です。特に今回は、メソッドに切り出して共通化するだけでも実現できましたが、後々の保守のことも考えてあえてインターフェースを用いた切り出し方にしました。それはなぜかというと、 `BattleContext` に元々あった機能と、新たに追加された機能のあいだの相互作用に気を配らなければならない可能性を排除するためでした。

## おわり

みんなもRPGつくろうね！