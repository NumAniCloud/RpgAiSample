using System;

class Program
{
    public static void Main()
    {
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
        var enemy3 = new EnemyBattler()
        {
            Hp = 100,
            Defense = 19,
        };

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

        context.View.Talk($"プレイヤーのHP:{context.Player.Hp}");
        context.View.Talk($"敵のHP:{context.Enemy.Hp}");

        while(true)
        {
            context.Player.Act(context);
            if(context.Enemy.Hp <= 0)
            {
                context.View.Talk("敵は倒れた！");
                context.View.Talk("プレイヤーの勝ち");
                return;
            }

            context.Enemy.Act(context);
            if(context.Player.Hp <= 0)
            {
                context.View.Talk("プレイヤーは倒れた！");
                context.View.Talk("敵の勝ち");
                return;
            }
        }
    }
}

class BattleContext
{
    public EnemyBattler Enemy { get; set; }
    public PlayerBattler Player { get; set; }
    public IView View { get; private set; }

    public BattleContext(IView view)
    {
        View = view;
    }
}

class Battler
{
    public int Hp { get; set; }
    public int Defense { get; set; }
}

class EnemyBattler : Battler
{
    public void Act(BattleContext context)
    {
        context.View.Talk("敵の攻撃");
        context.View.Talk("プレイヤーに 119 のダメージ");
        context.Player.Hp -= 119;
    }
}

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
        //Skills[1].Run(context, context.Enemy);
    }
}

abstract class Skill
{
    public abstract void Run(BattleContext context, Battler target);
}

sealed class SingleAttackSkill : Skill
{
    public int Power { get; private set; }

    public SingleAttackSkill(int power)
    {
        Power = power;
    }

    public override void Run(BattleContext context, Battler target)
    {
        context.View.Talk("あなたは狙いを定めて敵を撃ちぬいた！");

        var damage = Power - target.Defense;
        target.Hp -= damage;
        context.View.Talk($"敵に{damage}のダメージ！");
    }
}

sealed class TripleAttackSkill : Skill
{
    public int Power { get; private set; }

    public TripleAttackSkill(int power)
    {
        Power = power;
    }

    public override void Run(BattleContext context, Battler target)
    {
        context.View.Talk("あなたは敵の体へ銃を3連射した！");

        var singleDamage = Power - target.Defense;
        target.Hp -= singleDamage * 3;
        context.View.Talk($"敵に {singleDamage} のダメージ！");
        context.View.Talk($"敵に {singleDamage} のダメージ！");
        context.View.Talk($"敵に {singleDamage} のダメージ！");
    }
}

class PlayerAi
{
    public Skill DetermineSkill(BattleContext context, Skill[] skills)
    {
        (Skill, int priority) candidate = (null, -context.Enemy.Hp);

        // シミュレーション中に発動するスキルのメッセージを表示しないようにするためのクローン
        var cloneContext = new BattleContext(new NullView())
        {
            Enemy = context.Enemy,
            Player = context.Player
        };

        foreach (var skill in skills)
        {
            // シミュレーション中に敵が受けるダメージを実際には反映しないためのクローン
            // 割愛しているが、実際はプレイヤーのクローンも生成しておいたり、
            // バトラーのクローンはBattleContext.Enemyなどにもsetしておいたほうが
            // 独特なスキルをたくさん実装する際に安全
            var clone = new EnemyBattler()
            {
                Hp = context.Enemy.Hp,
                Defense = context.Enemy.Defense
            };

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

interface IView
{
    void Talk(string text);
}

class ConsoleView : IView
{
    public void Talk(string text)
    {
        Console.WriteLine(text);
    }
}

class NullView : IView
{
    public void Talk(string text)
    {
        // 何もしない
    }
}
