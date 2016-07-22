namespace LeagueSharp.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Forms;

    using LeagueSharp.SDK.Enumerations;
    using LeagueSharp.SDK.UI;
    using LeagueSharp.SDK.Utils;

    using SharpDX;

    using Color = System.Drawing.Color;
    using Menu = LeagueSharp.SDK.UI.Menu;

    /// <summary>
    ///     The orbwalking action event data.
    /// </summary>
    public class OrbwalkingActionArgs : EventArgs
    {
        #region Public Properties

        /// <summary>
        ///     Gets or sets the position.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether process.
        /// </summary>
        public bool Process { get; set; }

        /// <summary>
        ///     Gets the target.
        /// </summary>
        public AttackableUnit Target { get; internal set; }

        /// <summary>
        ///     Gets the type.
        /// </summary>
        public OrbwalkingType Type { get; internal set; }

        #endregion
    }

    /// <summary>
    ///     The <c>Orbwalk</c> system.
    /// </summary>
    public sealed class Orbwalker
    {
        #region Fields

        private readonly Menu mainMenu = new Menu("orbwalker", "Orbwalker");

        private readonly Random random = new Random(DateTime.Now.Millisecond);

        private readonly OrbwalkerSelector selector;

        private OrbwalkingMode activeMode = OrbwalkingMode.None;

        private List<Obj_AI_Minion> azirSoliders = new List<Obj_AI_Minion>();

        private int countAutoAttack;

        private bool enabled;

        private bool isRengarJumping;

        private bool isStartAttack, isFinishAttack;

        private Obj_AI_Base laneClearMinion;

        private int lastAutoAttackOrderTick, lastMovementOrderTick;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="Orbwalk" /> class.
        /// </summary>
        /// <param name="menu">The menu.</param>
        internal Orbwalker(Menu menu)
        {
            var drawing = new Menu("drawings", "Drawings");
            drawing.Add(new MenuBool("drawAARange", "Auto-Attack Range", true));
            drawing.Add(new MenuBool("drawAARangeEnemy", "Auto-Attack Range Enemy"));
            drawing.Add(new MenuBool("drawExtraHoldPosition", "Extra Hold Position"));
            drawing.Add(new MenuBool("drawKillableMinion", "Killable Minions"));
            drawing.Add(new MenuBool("drawKillableMinionFade", "Killable Minions Fade Effect"));
            this.mainMenu.Add(drawing);

            var advanced = new Menu("advanced", "Advanced");
            advanced.Add(new MenuSeparator("separatorMovement", "Movement"));
            advanced.Add(new MenuBool("movementMagnet", "Magnet To Target (Melee)"));
            advanced.Add(new MenuBool("movementRandomize", "Randomize Location", true));
            advanced.Add(new MenuSlider("movementExtraHold", "Extra Hold Position", 50, 0, 250));
            advanced.Add(new MenuSlider("movementMaximumDistance", "Maximum Distance", 1100, 500, 1500));
            advanced.Add(new MenuBool("movementHighAS", "Limit Kite if Attack Speed >= 2.5", true));

            advanced.Add(new MenuSeparator("separatorDelay", "Delay"));
            advanced.Add(new MenuSlider("delayMovement", "Movement", 100, 0, 500));
            advanced.Add(new MenuSlider("delayWindup", "Windup", 60, 0, 200));
            advanced.Add(new MenuSlider("delayFarm", "Farm", 30, 0, 200));

            advanced.Add(new MenuSeparator("separatorPrioritization", "Prioritization"));
            advanced.Add(new MenuBool("prioritizeFarm", "Farm Over Harass", true));
            advanced.Add(new MenuBool("prioritizeMinions", "Minions Over Objectives"));
            advanced.Add(new MenuBool("prioritizeSmallJungle", "Small Jungle"));
            advanced.Add(new MenuBool("prioritizeWards", "Wards"));
            advanced.Add(new MenuBool("prioritizeSpecialMinions", "Special Minions"));

            advanced.Add(new MenuSeparator("separatorAttack", "Attack"));
            advanced.Add(new MenuBool("attackWards", "Wards"));
            advanced.Add(new MenuBool("attackBarrels", "Barrels"));
            advanced.Add(new MenuBool("attackClones", "Clones"));
            advanced.Add(new MenuBool("attackSpecialMinions", "Special Minions", true));
            this.mainMenu.Add(advanced);

            this.mainMenu.Add(new MenuSeparator("separatorKeys", "Key Bindings"));
            this.mainMenu.Add(new MenuKeyBind("lasthitKey", "Last Hit", Keys.X, KeyBindType.Press));
            this.mainMenu.Add(new MenuKeyBind("laneclearKey", "Lane Clear", Keys.V, KeyBindType.Press));
            this.mainMenu.Add(new MenuKeyBind("hybridKey", "Hybrid", Keys.C, KeyBindType.Press));
            this.mainMenu.Add(new MenuKeyBind("comboKey", "Combo", Keys.Space, KeyBindType.Press));
            this.mainMenu.Add(new MenuBool("enabledOption", "Enabled", true));

            this.mainMenu.MenuValueChanged += (sender, args) =>
                {
                    var boolean = sender as MenuBool;

                    if (boolean != null && boolean.Name.Equals("enabledOption"))
                    {
                        this.Enabled = boolean.Value;
                    }
                };

            menu.Add(this.mainMenu);
            this.selector = new OrbwalkerSelector(this);

            Events.OnLoad += (sender, args) => { this.Enabled = this.mainMenu["enabledOption"]; };
        }

        #endregion

        #region Delegates

        /// <summary>
        ///     The<see cref="OnAction" /> event delegate.
        /// </summary>
        /// <param name="sender">
        ///     The sender
        /// </param>
        /// <param name="e">
        ///     The event data
        /// </param>
        public delegate void OnActionDelegate(object sender, OrbwalkingActionArgs e);

        #endregion

        #region Public Events

        /// <summary>
        ///     The OnAction event.
        /// </summary>
        public event OnActionDelegate OnAction;

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets or sets value indication in which mode Orbwalk should run.
        /// </summary>
        public OrbwalkingMode ActiveMode
        {
            get
            {
                return this.activeMode != OrbwalkingMode.None
                           ? this.activeMode
                           : (this.mainMenu["comboKey"].GetValue<MenuKeyBind>().Active
                                  ? OrbwalkingMode.Combo
                                  : (this.mainMenu["hybridKey"].GetValue<MenuKeyBind>().Active
                                         ? OrbwalkingMode.Hybrid
                                         : (this.mainMenu["laneclearKey"].GetValue<MenuKeyBind>().Active
                                                ? OrbwalkingMode.LaneClear
                                                : (this.mainMenu["lasthitKey"].GetValue<MenuKeyBind>().Active
                                                       ? OrbwalkingMode.LastHit
                                                       : OrbwalkingMode.None))));
            }
            set
            {
                this.activeMode = value;
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether movement.
        /// </summary>
        public bool AttackState { get; set; } = true;

        /// <summary>
        ///     Indicates whether the orbwalker can issue attacking.
        /// </summary>
        public bool CanAttack
        {
            get
            {
                switch (GameObjects.Player.ChampionName)
                {
                    case "Graves":
                        if (!GameObjects.Player.HasBuff("GravesBasicAttackAmmo1"))
                        {
                            return false;
                        }
                        break;
                    case "Jhin":
                        if (GameObjects.Player.HasBuff("JhinPassiveReload"))
                        {
                            return false;
                        }
                        break;
                }
                return GameObjects.Player.CanAttack && !GameObjects.Player.IsCastingInterruptableSpell()
                       && !GameObjects.Player.IsDashing()
                       && Variables.TickCount - this.lastAutoAttackOrderTick > 70 + Math.Min(60, Game.Ping)
                       && (!this.isStartAttack
                           || Variables.TickCount + 25 >= this.LastAutoAttackTick + this.AttackDelay * 1000);
            }
            private set
            {
                if (value)
                {
                    this.isStartAttack = false;
                    this.isFinishAttack = true;
                    this.LastAutoAttackTick = this.lastAutoAttackOrderTick = this.lastMovementOrderTick = 0;
                }
                else
                {
                    this.isStartAttack = true;
                    this.isFinishAttack = false;
                    this.LastAutoAttackTick = Variables.TickCount;
                    this.lastAutoAttackOrderTick += 70 + Math.Min(60, Game.Ping) - 5;
                    this.lastMovementOrderTick += Math.Max(0, this.mainMenu["advanced"]["delayMovement"] - 5);
                }
            }
        }

        /// <summary>
        ///     Indicates whether the orbwalker can issue moving.
        /// </summary>
        public bool CanMove
            =>
                GameObjects.Player.CanMove
                && (!GameObjects.Player.IsCastingInterruptableSpell()
                    || !GameObjects.Player.IsCastingInterruptableSpell(true))
                && Variables.TickCount - this.lastAutoAttackOrderTick > 70 + Math.Min(60, Game.Ping)
                && this.CanCancelAttack;

        /// <summary>
        ///     Gets a value indicating whether this <see cref="Orbwalker" /> is enabled.
        /// </summary>
        public bool Enabled
        {
            get
            {
                return this.enabled;
            }
            set
            {
                if (this.enabled != value)
                {
                    if (value)
                    {
                        Drawing.OnEndScene += this.OnEndScene;
                        GameObject.OnDelete += this.OnDelete;
                        Obj_AI_Base.OnProcessSpellCast += this.OnProcessSpellCast;
                        Spellbook.OnStopCast += this.OnStopCast;
                        Obj_AI_Base.OnDoCast += this.OnDoCast;
                        Obj_AI_Base.OnBuffAdd += this.OnBuffAdd;
                        Game.OnUpdate += this.OnUpdate;
                        switch (GameObjects.Player.ChampionName)
                        {
                            case "Azir":
                                this.azirSoliders =
                                    GameObjects.AllyMinions.Where(
                                        i => i.Name == "AzirSoldier" && i.HasBuff("azirwspawnsound")).ToList();
                                Obj_AI_Base.OnBuffAdd += this.AzirOnBuffAdd;
                                Obj_AI_Base.OnPlayAnimation += this.AzirOnPlayAnimation;
                                break;
                            case "Rengar":
                                Obj_AI_Base.OnNewPath += this.RengarOnNewPath;
                                Obj_AI_Base.OnPlayAnimation += this.RengarOnPlayAnimation;
                                break;
                            case "Riven":
                                Obj_AI_Base.OnPlayAnimation += this.RivenOnPlayAnimation;
                                break;
                        }
                    }
                    else
                    {
                        Drawing.OnEndScene -= this.OnEndScene;
                        GameObject.OnDelete -= this.OnDelete;
                        Obj_AI_Base.OnProcessSpellCast -= this.OnProcessSpellCast;
                        Spellbook.OnStopCast -= this.OnStopCast;
                        Obj_AI_Base.OnDoCast -= this.OnDoCast;
                        Obj_AI_Base.OnBuffAdd -= this.OnBuffAdd;
                        Game.OnUpdate -= this.OnUpdate;
                        switch (GameObjects.Player.ChampionName)
                        {
                            case "Azir":
                                Obj_AI_Base.OnBuffAdd -= this.AzirOnBuffAdd;
                                Obj_AI_Base.OnPlayAnimation -= this.AzirOnPlayAnimation;
                                this.azirSoliders = new List<Obj_AI_Minion>();
                                break;
                            case "Rengar":
                                Obj_AI_Base.OnNewPath -= this.RengarOnNewPath;
                                Obj_AI_Base.OnPlayAnimation -= this.RengarOnPlayAnimation;
                                break;
                            case "Riven":
                                Obj_AI_Base.OnPlayAnimation -= this.RivenOnPlayAnimation;
                                break;
                        }
                    }
                }

                this.enabled = value;

                if (this.mainMenu != null)
                {
                    this.mainMenu["enabledOption"].GetValue<MenuBool>().Value = this.enabled;
                }
            }
        }

        /// <summary>
        ///     Force orbwalker to orbwalk to a point. Set to null to stop forcing.
        /// </summary>
        public Vector3? ForceOrbwalkPoint { get; set; } = null;

        /// <summary>
        ///     Gets or sets the orbwalker's forced target.
        /// </summary>
        public AttackableUnit ForceTarget { get; set; }

        /// <summary>
        ///     Gets the last auto attack tick.
        /// </summary>
        public int LastAutoAttackTick { get; private set; }

        /// <summary>
        ///     Gets the last target.
        /// </summary>
        public AttackableUnit LastTarget { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether attack.
        /// </summary>
        public bool MovementState { get; set; } = true;

        #endregion

        #region Properties

        private float AttackDelay
            =>
                GameObjects.Player.ChampionName == "Graves"
                    ? 1.0740296828f * GameObjects.Player.AttackDelay - 0.7162381256175f
                    : GameObjects.Player.AttackDelay;

        private bool CanCancelAttack
        {
            get
            {
                var finishAtk = this.isFinishAttack;

                if (!GameObjects.Player.CanCancelAutoAttack())
                {
                    return finishAtk;
                }

                var extraWindUp = this.mainMenu["advanced"]["delayWindup"] + 25;
                switch (GameObjects.Player.ChampionName)
                {
                    case "Jinx":
                        extraWindUp += 100;
                        break;
                    case "Rengar":
                        if (GameObjects.Player.HasBuff("rengarqbase") || GameObjects.Player.HasBuff("rengarqemp"))
                        {
                            extraWindUp += 200;
                        }
                        break;
                }

                if (this.mainMenu["advanced"]["movementHighAS"] && (this.AttackDelay <= 1 / 2.5f)
                    && this.countAutoAttack % 2 != 0)
                {
                    extraWindUp = this.random.Next(100, 200);
                    finishAtk = false;
                }

                return finishAtk
                       || Variables.TickCount
                       >= this.LastAutoAttackTick + GameObjects.Player.AttackCastDelay * 1000 + extraWindUp;
            }
        }

        private float HoldRadius => GameObjects.Player.BoundingRadius + this.mainMenu["advanced"]["movementExtraHold"];

        private Vector3 OrbwalkPoint
        {
            get
            {
                if (this.ForceOrbwalkPoint.HasValue && this.ForceOrbwalkPoint.Value.IsValid())
                {
                    return this.ForceOrbwalkPoint.Value;
                }

                if (this.mainMenu["advanced"]["movementMagnet"] && this.LastTarget != null
                    && GameObjects.Player.IsMelee() && this.ActiveMode == OrbwalkingMode.Combo)
                {
                    var hero = this.LastTarget as Obj_AI_Hero;

                    if (hero.IsValidTarget() && hero.DistanceToPlayer() < hero.GetRealAutoAttackRange() + 150
                        && hero.Distance(Game.CursorPos) < Game.CursorPos.DistanceToPlayer()
                        && hero.Distance(Game.CursorPos) < 250)
                    {
                        return
                            Movement.GetPrediction(
                                hero,
                                GameObjects.Player.BasicAttack.SpellCastTime,
                                GameObjects.Player.GetRealAutoAttackRange(),
                                GameObjects.Player.BasicAttack.MissileSpeed).UnitPosition;
                    }
                }

                return Game.CursorPos;
            }
        }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Use orbwalker to attack
        /// </summary>
        /// <param name="target"></param>
        public void Attack(AttackableUnit target)
        {
            if (!this.CanAttack || GameObjects.Player.HasBuffOfType(BuffType.Blind))
            {
                return;
            }

            var gTarget = target ?? this.GetTarget();

            if (!gTarget.InAutoAttackRange())
            {
                return;
            }

            var eventArgs = new OrbwalkingActionArgs
                                { Target = gTarget, Process = true, Type = OrbwalkingType.BeforeAttack };
            this.InvokeAction(eventArgs);

            if (!eventArgs.Process)
            {
                return;
            }

            if (GameObjects.Player.IssueOrder(GameObjectOrder.AttackUnit, eventArgs.Target))
            {
                this.isStartAttack = false;
                this.lastAutoAttackOrderTick = Variables.TickCount;
                this.LastTarget = eventArgs.Target;
            }
        }

        /// <summary>
        ///     Get target for orbwalker
        /// </summary>
        /// <returns></returns>
        public AttackableUnit GetTarget()
        {
            return this.selector.GetTarget(this.ActiveMode);
        }

        /// <summary>
        ///     Use orbwalker to move
        /// </summary>
        /// <param name="position"></param>
        public void Move(Vector3 position)
        {
            if (!position.IsValid() || !this.CanMove
                || Variables.TickCount - this.lastMovementOrderTick < this.mainMenu["advanced"]["delayMovement"])
            {
                return;
            }

            if (position.Distance(GameObjects.Player.Position) < this.HoldRadius)
            {
                if (GameObjects.Player.Path.Length > 0)
                {
                    var eventStopArgs = new OrbwalkingActionArgs
                                            {
                                                Position = GameObjects.Player.ServerPosition, Process = true,
                                                Type = OrbwalkingType.StopMovement
                                            };
                    this.InvokeAction(eventStopArgs);

                    if (eventStopArgs.Process)
                    {
                        GameObjects.Player.IssueOrder(GameObjectOrder.Stop, eventStopArgs.Position);
                        this.lastMovementOrderTick = Variables.TickCount - 70;
                    }
                }

                return;
            }

            if (position.DistanceToPlayer() < GameObjects.Player.BoundingRadius)
            {
                position = GameObjects.Player.ServerPosition.Extend(
                    position,
                    GameObjects.Player.BoundingRadius + this.random.Next(0, 51));
            }

            if (position.DistanceToPlayer() > this.mainMenu["advanced"]["movementMaximumDistance"])
            {
                position = GameObjects.Player.ServerPosition.Extend(
                    position,
                    this.mainMenu["advanced"]["movementMaximumDistance"] + 25 - this.random.Next(0, 51));
            }

            if (this.mainMenu["advanced"]["movementRandomize"] && position.DistanceToPlayer() > 350)
            {
                var rAngle = 2 * Math.PI * this.random.NextDouble();
                var radius = GameObjects.Player.BoundingRadius / 2f;
                var x = (float)(position.X + radius * Math.Cos(rAngle));
                var y = (float)(position.Y + radius * Math.Sin(rAngle));
                position = new Vector3(x, y, NavMesh.GetHeightForPosition(x, y));
            }

            var angle = 0f;
            var currentPath = GameObjects.Player.GetWaypoints();

            if (currentPath.Count > 1 && currentPath.PathLength() > 100)
            {
                var movePath = GameObjects.Player.GetPath(position);

                if (movePath.Length > 1)
                {
                    angle = (currentPath[1] - currentPath[0]).AngleBetween(movePath[1] - movePath[0]);
                    var distance = movePath.Last().DistanceSquared(currentPath.Last());

                    if ((angle < 10 && distance < 500 * 500) || distance < 50 * 50)
                    {
                        return;
                    }
                }
            }

            if (angle < 60
                    ? Variables.TickCount - this.lastMovementOrderTick < 70 + Math.Min(60, Game.Ping)
                    : Variables.TickCount - this.lastMovementOrderTick < 60)
            {
                return;
            }

            var eventArgs = new OrbwalkingActionArgs
                                { Position = position, Process = true, Type = OrbwalkingType.Movement };
            this.InvokeAction(eventArgs);

            if (!eventArgs.Process)
            {
                return;
            }

            if (GameObjects.Player.IssueOrder(GameObjectOrder.MoveTo, eventArgs.Position))
            {
                this.lastMovementOrderTick = Variables.TickCount;
            }
        }

        /// <summary>
        ///     <c>Orbwalk</c> command, attempting to attack or move.
        /// </summary>
        /// <param name="target">
        ///     The target of choice
        /// </param>
        /// <param name="position">
        ///     The position of choice
        /// </param>
        public void Orbwalk(AttackableUnit target = null, Vector3? position = null)
        {
            if (this.AttackState)
            {
                this.Attack(target);
            }

            if (this.MovementState)
            {
                this.Move(position.HasValue && position.Value.IsValid() ? position.Value : this.OrbwalkPoint);
            }
        }

        /// <summary>
        ///     Resets the swing timer.
        /// </summary>
        public void ResetSwingTimer()
        {
            this.CanAttack = true;
        }

        #endregion

        #region Methods

        private void AzirOnBuffAdd(Obj_AI_Base sender, Obj_AI_BaseBuffAddEventArgs args)
        {
            var solider = sender as Obj_AI_Minion;

            if (solider != null && solider.IsAlly && solider.Name == "AzirSoldier"
                && args.Buff.DisplayName.ToLower() == "azirwspawnsound")
            {
                this.azirSoliders.Add(solider);
            }
        }

        private void AzirOnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            var solider = sender as Obj_AI_Minion;

            if (solider != null && solider.IsAlly && solider.Name == "AzirSolider" && args.Animation == "Death")
            {
                var index = this.azirSoliders.FindIndex(i => i.Compare(solider));

                if (index != -1)
                {
                    this.azirSoliders.RemoveAt(index);
                }
            }
        }

        private void InvokeAction(OrbwalkingActionArgs e)
        {
            this.OnAction?.Invoke(MethodBase.GetCurrentMethod().DeclaringType, e);
        }

        private void InvokeActionAfterAttack()
        {
            if (Game.Ping <= 30)
            {
                DelayAction.Add(30 - Game.Ping, this.InvokeActionAfterAttackDelay);
            }
            else
            {
                this.InvokeActionAfterAttackDelay();
            }
        }

        private void InvokeActionAfterAttackDelay()
        {
            if (this.isFinishAttack)
            {
                return;
            }

            this.InvokeAction(new OrbwalkingActionArgs { Target = this.LastTarget, Type = OrbwalkingType.AfterAttack });
            this.isFinishAttack = true;
        }

        private void InvokeActionOnAttack(AttackableUnit target)
        {
            this.CanAttack = false;
            var unit = target ?? this.LastTarget;

            if (unit == null)
            {
                return;
            }

            this.countAutoAttack++;
            this.LastTarget = unit;
            this.InvokeAction(new OrbwalkingActionArgs { Target = unit, Type = OrbwalkingType.OnAttack });
        }

        private void OnBuffAdd(Obj_AI_Base sender, Obj_AI_BaseBuffAddEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.Buff.DisplayName == "SonaPassiveReady")
            {
                this.ResetSwingTimer();
            }
        }

        private void OnDelete(GameObject sender, EventArgs args)
        {
            if (sender.Compare(this.laneClearMinion))
            {
                this.laneClearMinion = null;
            }
            else if (sender.Compare(this.LastTarget))
            {
                this.LastTarget = null;
            }
            else if (sender.Compare(this.ForceTarget))
            {
                this.ForceTarget = null;
            }
        }

        private void OnDoCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (AutoAttack.IsAutoAttack(args.SData.Name))
            {
                this.InvokeActionAfterAttack();
            }
            else if (AutoAttack.IsAutoAttackReset(args.SData.Name))
            {
                DelayAction.Add(30, this.ResetSwingTimer);
            }
        }

        private void OnEndScene(EventArgs args)
        {
            if (GameObjects.Player.IsDead)
            {
                return;
            }

            if (this.mainMenu["drawings"]["drawAARange"]
                && GameObjects.Player.Position.IsOnScreen(GameObjects.Player.GetRealAutoAttackRange()))
            {
                Render.Circle.DrawCircle(
                    GameObjects.Player.Position,
                    GameObjects.Player.GetRealAutoAttackRange(),
                    Color.PaleGreen);
            }

            if (this.mainMenu["drawings"]["drawExtraHoldPosition"]
                && GameObjects.Player.Position.IsOnScreen(this.HoldRadius))
            {
                Render.Circle.DrawCircle(GameObjects.Player.Position, this.HoldRadius, Color.Purple);
            }

            if (this.mainMenu["drawings"]["drawAARangeEnemy"])
            {
                foreach (var enemy in
                    GameObjects.EnemyHeroes.Where(
                        e => e.IsValidTarget() && e.Position.IsOnScreen(e.GetRealAutoAttackRange(GameObjects.Player))))
                {
                    Render.Circle.DrawCircle(
                        enemy.Position,
                        enemy.GetRealAutoAttackRange(GameObjects.Player),
                        Color.PaleVioletRed);
                }
            }

            if (this.mainMenu["drawings"]["drawKillableMinion"])
            {
                if (this.mainMenu["drawings"]["drawKillableMinionFade"])
                {
                    var minions =
                        this.selector.GetEnemyMinions(GameObjects.Player.GetRealAutoAttackRange() * 2f)
                            .Where(
                                m =>
                                m.Position.IsOnScreen() && m.Health < GameObjects.Player.GetAutoAttackDamage(m) * 2f);
                    foreach (var minion in minions)
                    {
                        Render.Circle.DrawCircle(
                            minion.Position,
                            minion.BoundingRadius * 2f,
                            Color.FromArgb(
                                255,
                                0,
                                255,
                                (byte)(255 - Math.Max(Math.Min(255 - minion.Health * 2, 255), 0))));
                    }
                }
                else
                {
                    var minions =
                        this.selector.GetEnemyMinions(GameObjects.Player.GetRealAutoAttackRange() * 2f)
                            .Where(m => m.Position.IsOnScreen() && m.Health < GameObjects.Player.GetAutoAttackDamage(m));
                    foreach (var minion in minions)
                    {
                        Render.Circle.DrawCircle(
                            minion.Position,
                            minion.BoundingRadius * 2f,
                            Color.FromArgb(255, 0, 255, 0));
                    }
                }
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (AutoAttack.IsAutoAttack(args.SData.Name))
            {
                var target = args.Target as AttackableUnit;

                if (target != null && target.IsValid)
                {
                    this.InvokeActionOnAttack(target);
                }
            }
            else if (AutoAttack.IsAutoAttackReset(args.SData.Name) && !this.isRengarJumping)
            {
                DelayAction.Add(30, this.ResetSwingTimer);
            }
        }

        private void OnStopCast(Spellbook spellbook, SpellbookStopCastEventArgs args)
        {
            if (!spellbook.Owner.IsMe)
            {
                return;
            }

            if (args.DestroyMissile && args.StopAnimation)
            {
                this.ResetSwingTimer();
            }
        }

        private void OnUpdate(EventArgs args)
        {
            if (this.LastTarget != null && !this.LastTarget.IsValidTarget())
            {
                this.LastTarget = null;
            }

            if (!this.CanMove && this.LastTarget == null)
            {
                this.isFinishAttack = true;
            }

            if (GameObjects.Player.IsDead || MenuGUI.IsShopOpen || this.ActiveMode == OrbwalkingMode.None)
            {
                return;
            }

            this.Orbwalk();
        }

        private void RengarOnNewPath(Obj_AI_Base sender, GameObjectNewPathEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (this.isRengarJumping && !args.IsDash)
            {
                this.isRengarJumping = false;
                this.InvokeActionAfterAttack();
            }
        }

        private void RengarOnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.Animation == "Spell5")
            {
                this.isRengarJumping = true;
                this.InvokeActionOnAttack(null);
            }
        }

        private void RivenOnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            if (!sender.IsMe)
            {
                return;
            }

            if (args.Animation.Contains("Spell1") && this.ActiveMode != OrbwalkingMode.None)
            {
                DelayAction.Add(
                    args.Animation.EndsWith("c") ? 300 + Game.Ping : 250 + Game.Ping,
                    () =>
                        {
                            Game.SendEmote(Emote.Dance);
                            this.ResetSwingTimer();
                            GameObjects.Player.IssueOrder(
                                GameObjectOrder.MoveTo,
                                GameObjects.Player.Position.Extend(Game.CursorPos, -10));
                        });
            }
        }

        #endregion

        private class OrbwalkerSelector
        {
            #region Constants

            private const float LaneClearWaitTime = 2f;

            #endregion

            #region Fields

            private readonly string[] clones = { "shaco", "monkeyking", "leblanc" };

            private readonly string[] ignoreMinions = { "jarvanivstandard" };

            private readonly Orbwalker orbwalk;

            private readonly string[] specialMinions =
                {
                    "zyrathornplant", "zyragraspingplant", "heimertyellow",
                    "heimertblue", "malzaharvoidling", "yorickdecayedghoul",
                    "yorickravenousghoul", "yorickspectralghoul", "shacobox",
                    "annietibbers", "teemomushroom", "elisespiderling"
                };

            #endregion

            #region Constructors and Destructors

            /// <summary>
            ///     Initializes a new instance of the <see cref="Orbwalker.selector" /> class.
            /// </summary>
            /// <param name="orbwalk">
            ///     The orbwalker.
            /// </param>
            public OrbwalkerSelector(Orbwalker orbwalk)
            {
                this.orbwalk = orbwalk;
            }

            #endregion

            #region Properties

            private int FarmDelay => this.orbwalk.mainMenu["advanced"]["delayFarm"];

            #endregion

            #region Public Methods and Operators

            /// <summary>
            ///     Gets the enemy minions.
            /// </summary>
            /// <param name="range">
            ///     The range.
            /// </param>
            /// <returns>
            ///     The <see cref="List{T}" /> of <see cref="Obj_AI_Minion" />.
            /// </returns>
            public List<Obj_AI_Minion> GetEnemyMinions(float range = 0)
            {
                return
                    GameObjects.EnemyMinions.Where(
                        m => IsValidUnit(m, range) && !this.ignoreMinions.Any(b => b.Equals(m.CharData.BaseSkinName)))
                        .ToList();
            }

            /// <summary>
            ///     Gets the target.
            /// </summary>
            /// <param name="mode">
            ///     The mode.
            /// </param>
            /// <returns>
            ///     Returns the filtered target.
            /// </returns>
            public AttackableUnit GetTarget(OrbwalkingMode mode)
            {
                if ((mode == OrbwalkingMode.Hybrid || mode == OrbwalkingMode.LaneClear)
                    && !this.orbwalk.mainMenu["advanced"]["prioritizeFarm"])
                {
                    var target = Variables.TargetSelector.GetTarget(-1f, DamageType.Physical);

                    if (target.InAutoAttackRange())
                    {
                        return target;
                    }
                }

                var minions = new List<Obj_AI_Minion>();

                if (mode != OrbwalkingMode.None)
                {
                    minions = this.GetMinions(mode);
                }

                // Killable Minion
                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Hybrid || mode == OrbwalkingMode.LastHit)
                {
                    foreach (var minion in minions.OrderBy(m => m.Health))
                    {
                        if (minion.IsHPBarRendered && minion.Health < GameObjects.Player.GetAutoAttackDamage(minion))
                        {
                            return minion;
                        }

                        if (minion.MaxHealth <= 10)
                        {
                            if (minion.Health <= 1)
                            {
                                return minion;
                            }
                        }
                        else
                        {
                            var predHealth = Health.GetPrediction(minion, (int)minion.GetTimeToHit(), this.FarmDelay);

                            if (predHealth <= 0)
                            {
                                this.orbwalk.InvokeAction(
                                    new OrbwalkingActionArgs
                                        { Target = minion, Type = OrbwalkingType.NonKillableMinion });
                            }
                            else if (predHealth > 0 && predHealth < GameObjects.Player.GetAutoAttackDamage(minion))
                            {
                                return minion;
                            }
                        }
                    }
                }

                // Forced Target
                if (this.orbwalk.ForceTarget.InAutoAttackRange())
                {
                    return this.orbwalk.ForceTarget;
                }

                // Turrets | Inhibitors | Nexus
                if (mode == OrbwalkingMode.LaneClear
                    && (!this.orbwalk.mainMenu["advanced"]["prioritizeMinions"] || !minions.Any()))
                {
                    foreach (var turret in GameObjects.EnemyTurrets.Where(t => t.InAutoAttackRange()))
                    {
                        return turret;
                    }

                    foreach (var inhib in
                        GameObjects.EnemyInhibitors.Where(i => i.InAutoAttackRange()))
                    {
                        return inhib;
                    }

                    if (GameObjects.EnemyNexus.InAutoAttackRange())
                    {
                        return GameObjects.EnemyNexus;
                    }
                }

                // Champions
                if (mode != OrbwalkingMode.LastHit)
                {
                    var target = Variables.TargetSelector.GetTarget(-1f, DamageType.Physical);

                    if (target.InAutoAttackRange())
                    {
                        return target;
                    }
                }

                // Jungle Minions
                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Hybrid)
                {
                    var minion = minions.FirstOrDefault(m => m.Team == GameObjectTeam.Neutral);

                    if (minion != null)
                    {
                        return minion;
                    }
                }

                // Under-Turret Farming
                if (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Hybrid || mode == OrbwalkingMode.LastHit)
                {
                    Obj_AI_Minion farmUnderTurretMinion = null;
                    Obj_AI_Minion noneKillableMinion = null;

                    // return all the minions under turret
                    var turretMinions = minions.Where(m => m.IsMinion() && m.Position.IsUnderAllyTurret()).ToList();

                    if (turretMinions.Any())
                    {
                        // get the turret aggro minion
                        var turretMinion = turretMinions.FirstOrDefault(Health.HasTurretAggro);

                        if (turretMinion != null)
                        {
                            var hpLeftBeforeDie = 0;
                            var hpLeft = 0;
                            var turretAttackCount = 0;
                            var turret = Health.GetAggroTurret(turretMinion);

                            if (turret != null)
                            {
                                var turretStarTick = Health.TurretAggroStartTick(turretMinion);
                                var turretLandTick = turretStarTick + (int)(turret.AttackCastDelay * 1000)
                                                     + 1000
                                                     * Math.Max(
                                                         0,
                                                         (int)(turretMinion.Distance(turret) - turret.BoundingRadius))
                                                     / (int)(turret.BasicAttack.MissileSpeed + 70);

                                // calculate the HP before try to balance it
                                for (float i = turretLandTick + 50;
                                     i < turretLandTick + 3 * turret.AttackDelay * 1000 + 50;
                                     i = i + turret.AttackDelay * 1000)
                                {
                                    var time = (int)i - Variables.TickCount + Game.Ping / 2;
                                    var predHp =
                                        (int)
                                        Health.GetPrediction(
                                            turretMinion,
                                            time > 0 ? time : 0,
                                            70,
                                            HealthPredictionType.Simulated);

                                    if (predHp > 0)
                                    {
                                        hpLeft = predHp;
                                        turretAttackCount += 1;
                                        continue;
                                    }

                                    hpLeftBeforeDie = hpLeft;
                                    hpLeft = 0;
                                    break;
                                }

                                // calculate the hits is needed and possibilty to balance
                                if (hpLeft == 0 && turretAttackCount != 0 && hpLeftBeforeDie != 0)
                                {
                                    var damage = (int)GameObjects.Player.GetAutoAttackDamage(turretMinion);
                                    var hits = hpLeftBeforeDie / damage;
                                    var timeBeforeDie = turretLandTick
                                                        + (turretAttackCount + 1) * (int)(turret.AttackDelay * 1000)
                                                        - Variables.TickCount;
                                    var timeUntilAttackReady = this.orbwalk.LastAutoAttackTick
                                                               + (int)(this.orbwalk.AttackDelay * 1000)
                                                               > Variables.TickCount + 60
                                                                   ? this.orbwalk.LastAutoAttackTick
                                                                     + (int)(this.orbwalk.AttackDelay * 1000)
                                                                     - (Variables.TickCount + 60)
                                                                   : 0;
                                    var timeToLandAttack = turretMinion.GetTimeToHit();

                                    if (hits >= 1
                                        && hits * this.orbwalk.AttackDelay * 1000 + timeUntilAttackReady
                                        + timeToLandAttack < timeBeforeDie)
                                    {
                                        farmUnderTurretMinion = turretMinion;
                                    }
                                    else if (hits >= 1
                                             && hits * this.orbwalk.AttackDelay * 1000 + timeUntilAttackReady
                                             + timeToLandAttack > timeBeforeDie)
                                    {
                                        noneKillableMinion = turretMinion;
                                    }
                                }
                                else if (hpLeft == 0 && turretAttackCount == 0 && hpLeftBeforeDie == 0)
                                {
                                    noneKillableMinion = turretMinion;
                                }

                                // should wait before attacking a minion.
                                if (this.ShouldWaitUnderTurret(noneKillableMinion))
                                {
                                    return null;
                                }

                                if (farmUnderTurretMinion != null)
                                {
                                    return farmUnderTurretMinion;
                                }

                                // balance other minions
                                return
                                    (from minion in
                                         turretMinions.Where(
                                             x => x.NetworkId != turretMinion.NetworkId && !Health.HasMinionAggro(x))
                                     where
                                         (int)minion.Health % (int)turret.GetAutoAttackDamage(minion)
                                         > (int)GameObjects.Player.GetAutoAttackDamage(minion)
                                     select minion).FirstOrDefault();
                            }
                        }
                        else
                        {
                            if (this.ShouldWaitUnderTurret())
                            {
                                return null;
                            }

                            // balance other minions
                            return (from minion in turretMinions.Where(x => !Health.HasMinionAggro(x))
                                    let turret =
                                        GameObjects.AllyTurrets.FirstOrDefault(
                                            x => x.IsValidTarget(950, false, minion.Position))
                                    where
                                        turret != null
                                        && (int)minion.Health % (int)turret.GetAutoAttackDamage(minion)
                                        > (int)GameObjects.Player.GetAutoAttackDamage(minion)
                                    select minion).FirstOrDefault();
                        }

                        return null;
                    }
                }

                // Lane Clear Minions
                if (mode == OrbwalkingMode.LaneClear)
                {
                    if (!this.ShouldWait())
                    {
                        if (this.orbwalk.laneClearMinion.InAutoAttackRange())
                        {
                            if (this.orbwalk.laneClearMinion.MaxHealth <= 10)
                            {
                                return this.orbwalk.laneClearMinion;
                            }

                            var predHealth = Health.GetPrediction(
                                this.orbwalk.laneClearMinion,
                                (int)(this.orbwalk.AttackDelay * 1000 * LaneClearWaitTime),
                                this.FarmDelay,
                                HealthPredictionType.Simulated);

                            if (predHealth >= 2 * GameObjects.Player.GetAutoAttackDamage(this.orbwalk.laneClearMinion)
                                || Math.Abs(predHealth - this.orbwalk.laneClearMinion.Health) < float.Epsilon)
                            {
                                return this.orbwalk.laneClearMinion;
                            }
                        }

                        foreach (var minion in minions.Where(m => m.Team != GameObjectTeam.Neutral))
                        {
                            if (minion.MaxHealth <= 10)
                            {
                                this.orbwalk.laneClearMinion = minion;
                                return minion;
                            }

                            var predHealth = Health.GetPrediction(
                                minion,
                                (int)(this.orbwalk.AttackDelay * 1000 * LaneClearWaitTime),
                                this.FarmDelay,
                                HealthPredictionType.Simulated);

                            if (predHealth >= 2 * GameObjects.Player.GetAutoAttackDamage(minion)
                                || Math.Abs(predHealth - minion.Health) < float.Epsilon)
                            {
                                this.orbwalk.laneClearMinion = minion;
                                return minion;
                            }
                        }
                    }
                }

                // Special Minions if no enemy is near
                if (mode == OrbwalkingMode.Combo && minions.Any()
                    && !GameObjects.EnemyHeroes.Any(e => e.IsValidTarget(e.GetRealAutoAttackRange() * 2f)))
                {
                    return minions.FirstOrDefault();
                }

                return null;
            }

            #endregion

            #region Methods

            private static bool IsValidUnit(AttackableUnit unit, float range = 0f)
            {
                return unit.IsValidTarget()
                       && unit.Distance(GameObjects.Player) < (range > 0 ? range : unit.GetRealAutoAttackRange());
            }

            private static List<Obj_AI_Minion> OrderEnemyMinions(IEnumerable<Obj_AI_Minion> minions)
            {
                return
                    minions?.OrderByDescending(minion => minion.GetMinionType().HasFlag(MinionTypes.Siege))
                        .ThenBy(minion => minion.GetMinionType().HasFlag(MinionTypes.Super))
                        .ThenBy(minion => minion.Health)
                        .ThenByDescending(minion => minion.MaxHealth)
                        .ToList();
            }

            private List<Obj_AI_Minion> GetMinions(OrbwalkingMode mode)
            {
                var minions = mode != OrbwalkingMode.Combo;
                var attackWards = this.orbwalk.mainMenu["advanced"]["attackWards"];
                var attackClones = this.orbwalk.mainMenu["advanced"]["attackClones"];
                var attackSpecialMinions = this.orbwalk.mainMenu["advanced"]["attackSpecialMinions"];
                var prioritizeWards = this.orbwalk.mainMenu["advanced"]["prioritizeWards"];
                var prioritizeSpecialMinions = this.orbwalk.mainMenu["advanced"]["prioritizeSpecialMinions"];
                var minionList = new List<Obj_AI_Minion>();
                var specialList = new List<Obj_AI_Minion>();
                var cloneList = new List<Obj_AI_Minion>();
                var wardList = new List<Obj_AI_Minion>();
                foreach (var minion in
                    GameObjects.EnemyMinions.Where(m => IsValidUnit(m)))
                {
                    var baseName = minion.CharData.BaseSkinName.ToLower();

                    if (minions && minion.IsMinion())
                    {
                        minionList.Add(minion);
                    }
                    else if (attackSpecialMinions && this.specialMinions.Any(s => s.Equals(baseName)))
                    {
                        specialList.Add(minion);
                    }
                    else if (attackClones && this.clones.Any(c => c.Equals(baseName)))
                    {
                        cloneList.Add(minion);
                    }
                }

                if (minions)
                {
                    minionList = OrderEnemyMinions(minionList);
                    minionList.AddRange(
                        this.OrderJungleMinions(
                            GameObjects.Jungle.Where(
                                j => IsValidUnit(j) && !j.CharData.BaseSkinName.Equals("gangplankbarrel")).ToList()));
                }

                if (attackWards)
                {
                    wardList.AddRange(GameObjects.EnemyWards.Where(w => IsValidUnit(w)));
                }

                var finalMinionList = new List<Obj_AI_Minion>();

                if (attackWards && prioritizeWards && attackSpecialMinions && prioritizeSpecialMinions)
                {
                    finalMinionList.AddRange(wardList);
                    finalMinionList.AddRange(specialList);
                    finalMinionList.AddRange(minionList);
                }
                else if ((!attackWards || !prioritizeWards) && attackSpecialMinions && prioritizeSpecialMinions)
                {
                    finalMinionList.AddRange(specialList);
                    finalMinionList.AddRange(minionList);
                    finalMinionList.AddRange(wardList);
                }
                else if (attackWards && prioritizeWards)
                {
                    finalMinionList.AddRange(wardList);
                    finalMinionList.AddRange(minionList);
                    finalMinionList.AddRange(specialList);
                }
                else
                {
                    finalMinionList.AddRange(minionList);
                    finalMinionList.AddRange(specialList);
                    finalMinionList.AddRange(wardList);
                }

                if (this.orbwalk.mainMenu["advanced"]["attackBarrels"])
                {
                    finalMinionList.AddRange(
                        GameObjects.Jungle.Where(
                            j => IsValidUnit(j) && j.Health <= 1 && j.CharData.BaseSkinName.Equals("gangplankbarrel"))
                            .ToList());
                }

                if (attackClones)
                {
                    finalMinionList.AddRange(cloneList);
                }

                return
                    finalMinionList.Where(m => !this.ignoreMinions.Any(b => b.Equals(m.CharData.BaseSkinName))).ToList();
            }

            private List<Obj_AI_Minion> OrderJungleMinions(List<Obj_AI_Minion> minions)
            {
                return
                    (this.orbwalk.mainMenu["advanced"]["prioritizeSmallJungle"]
                         ? minions.OrderBy(m => m.MaxHealth)
                         : minions.OrderByDescending(m => m.MaxHealth)).ToList();
            }

            private bool ShouldWait()
            {
                return
                    this.GetEnemyMinions()
                        .Any(
                            m =>
                            Health.GetPrediction(
                                m,
                                (int)(this.orbwalk.AttackDelay * 1000 * LaneClearWaitTime),
                                this.FarmDelay,
                                HealthPredictionType.Simulated) < GameObjects.Player.GetAutoAttackDamage(m));
            }

            private bool ShouldWaitUnderTurret(Obj_AI_Minion noneKillableMinion = null)
            {
                return
                    this.GetEnemyMinions()
                        .Any(
                            m =>
                            (noneKillableMinion == null || noneKillableMinion.NetworkId != m.NetworkId)
                            && m.InAutoAttackRange()
                            && Health.GetPrediction(
                                m,
                                (int)(this.orbwalk.AttackDelay * 1000 + m.GetTimeToHit()),
                                this.FarmDelay,
                                HealthPredictionType.Simulated) < GameObjects.Player.GetAutoAttackDamage(m));
            }

            #endregion
        }
    }
}