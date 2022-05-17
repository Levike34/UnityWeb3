using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.Contracts.Services;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.UnityClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3.Accounts;
using Nethereum.Web3;


public class Player : MonoBehaviour {
    public CoinHandler coinhandler;
    public bool showStoreUi;
    //With this variables we can disable melee or ranged combat
    [Header("FUNCTIONS")]
    public bool canJump;
    public bool canSlide;
    public bool canUseSword;
    public bool canUseBow;

    //How much health does our player character have
    [Header("HEALTH")]
    public int currentHealth;
    private int maximumHealth;
    public Image healthBar;
    public uint coinBalance;
    public uint spellCasts;
    public bool did;
 

    [Header("MOVING")]
    public bool canControl = true; //With this we can disable/enable control over player character
    private bool resetMovement = false;
    public float movingSpeed = 1.0f; //At what speed our player character will move
    private float inputX; //This and next variable are used to determine if we need to flip our characters sprite
    private bool flipped = false;
    public float jumpingForce = 2.0f; //At what force will we make our characters rigidbody to jump on Y axis
    private int jumpCount = 0; //This is used for counting jump button presses for double jump feature
    public float slideForce = 2.0f; //This is force at what will rigidbody move when sliding with character
    public float slideRate = 1.0f;
    private bool sliding = false;
    private float nextslideTime = 0.0f;
    public LayerMask groundLayer; //Layer of ground/terrain
    private bool player_grounded = false;
    public LayerMask waterLayer; //Layer of water for swimming
    private bool player_in_water = false;

    [Header("COMBAT")]
    public int damage; //How much damage does our player character deal to AI
    public float meleeRate = 1.5f; //At what rate can player character attack
    private float nextAttackTime = 0.0f;
    [HideInInspector]
    public bool blocking = false; //Used to keep track if our player character is blocking so we can block AI attack
    public int arrowDamage; //How much damage does ranged attack deal to AI
    public GameObject arrowPrefab; //Prefab of projectile used when attacking from range
    public Transform arrowPoint; //Point from where we instantiate arrowPrefab

    [Header("FX")]
    public GameObject effectsPrefab; //These variables are used for setting dust particles that will play when player touches the ground
    private bool dustImpact = false;
    private bool waterSplashed = false;

    [Header("LEDGE AND GRABING")]
    public Transform[] ledgeDetector; //Transforms that helps us when detecting if there is ledge for climbing
    public float ledgeDetectDistance = 1.0f; //At what distance do we detect if there is ledge or not
    private bool onLedge = false; //Used to keep track if player character is already holding on ledge
    public Vector2 ledgePosMod = new Vector2(-0.0825f, -0.125f); //Modification of ledge position for better alignment
    private Vector2 ledgePos;
    private RaycastHit2D verticalDetect;
    private RaycastHit2D horizontalDetect;
    private RaycastHit2D downDetect;
    private RaycastHit2D findPointRaycast;

    [HideInInspector]
    public CapsuleCollider2D checkGroundHelpCollider;
    [HideInInspector]
    public Animator player_animator;
    [HideInInspector]
    public Rigidbody2D player_rigidbody2d;
    [HideInInspector]
    public SFXPlayer sfxPlayer;
     
    PlayerInputActions input;
    Vector2 movementInput;
    float jump;
    float crouch;
    float slamAttack;
    float attack;
    float fire_bow;
    float block;
    float slide;
    bool chat;

    public void Awake() {

        if (Camera.main) {
            GameObject sceneCam = Camera.main.gameObject;
            if (sceneCam.GetComponentInChildren<Cinemachine.CinemachineVirtualCamera>()) {
                sceneCam.GetComponentInChildren<Cinemachine.CinemachineVirtualCamera>().Follow = transform;
            }
        }

        input = new PlayerInputActions();
        input.PlayerControls.Move.performed += ctx => movementInput = ctx.ReadValue<Vector2>();
        input.PlayerControls.Jump.performed += ctx => jump = ctx.ReadValue<float>();
        input.PlayerControls.Attack.performed += ctx => attack = ctx.ReadValue<float>();
        input.PlayerControls.FireBow.performed += ctx => fire_bow = ctx.ReadValue<float>();
        input.PlayerControls.Crouch.performed += ctx => crouch = ctx.ReadValue<float>();
        input.PlayerControls.Crouch.performed += ctx => slamAttack = ctx.ReadValue<float>();
        input.PlayerControls.Block.performed += ctx => block = ctx.ReadValue<float>();
        input.PlayerControls.Slide.performed += ctx => slide = ctx.ReadValue<float>();

    }

    public void Start() {
        //Here we are getting characters components
        checkGroundHelpCollider = GetComponent<CapsuleCollider2D>();
        player_animator = GetComponent<Animator>();
        player_rigidbody2d = GetComponent<Rigidbody2D>();
        sfxPlayer = GetComponent<SFXPlayer>();

        //Setting maximum health as current health
        maximumHealth = currentHealth;

        //Disabling cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Update()
    {
        if (currentHealth <= 0)
        {

            // If current health drops bellow zero we want it to stay at zero and we also are going to set Dead animation trigger
            currentHealth = 0;
            player_animator.SetBool("Dead", true);
        }
        else if (currentHealth > 0)
        { // If player characters health are above zero
            if (!onLedge)
            { // If player character is not holding on ledge
                if (canControl)
                { // If player character can be controlled
                    #region SET GROUNDIND, SWIMMING AND RESET THINGS
                    if (checkGroundHelpCollider.IsTouchingLayers(waterLayer))
                    { //Ground is checked with capsule collider component
                        player_in_water = true;

                        if (!waterSplashed)
                        { //We use this boolean to reset everything that we need for character to swim
                            player_rigidbody2d.gravityScale = 0.0f;
                            player_rigidbody2d.velocity = Vector2.zero;
                            player_animator.SetBool("InWater", true);

                            var pos = new Vector2(transform.position.x, transform.position.y + 0.15f);
                            GameObject effectGo = Instantiate(effectsPrefab, pos, Quaternion.identity);
                            effectGo.GetComponent<Effect>().effectsAnimator.SetInteger("EffectsState", 5);
                            sfxPlayer.PlaySFX(AudioClipType.WaterImpact);
                            waterSplashed = true;
                        }
                    }
                    else
                    {
                        player_in_water = false;

                        if (waterSplashed)
                        {
                            player_animator.SetBool("InWater", false);
                            player_rigidbody2d.gravityScale = 0.4f;

                            var pos = new Vector2(transform.position.x, transform.position.y + 0.15f);
                            GameObject effectGo = Instantiate(effectsPrefab, pos, Quaternion.identity);
                            effectGo.GetComponent<Effect>().effectsAnimator.SetInteger("EffectsState", 5);
                            sfxPlayer.PlaySFX(AudioClipType.WaterImpact);
                            waterSplashed = false;
                        }
                    }

                    if (checkGroundHelpCollider.IsTouchingLayers(groundLayer))
                    { //Here we see character is on groundLayer variable
                        player_grounded = true;
                        jumpCount = 0;

                        if (!dustImpact)
                        { //This is used to instantiate dust particle effect
                            GameObject effectGo = Instantiate(effectsPrefab, transform.position, Quaternion.identity);
                            effectGo.GetComponent<Effect>().effectsAnimator.SetInteger("EffectsState", 1);
                            sfxPlayer.PlaySFX(AudioClipType.GroundImpact);
                            dustImpact = true;
                        }
                    }
                    else
                    {
                        player_grounded = false;
                        dustImpact = false;

                        if (!player_in_water)
                        { //We want to be able to detect ledge only if we are not in water
                            DetectLedge();
                        }
                    }
                    #endregion

                    #region AXIS AND FLIPPING
                    inputX = movementInput.x; //This is input for moving left or right

                    //Here we are simply and literally rotating character based on his movement direction
                    if (inputX > 0)
                    {
                        transform.localEulerAngles = new Vector2(0f, 0f);
                        flipped = false;
                    }
                    else if (inputX < 0)
                    {
                        transform.localEulerAngles = new Vector2(0f, 180f);
                        flipped = true;
                    }
                    #endregion

                    if (!player_in_water)
                    {
                        #region MOVEMENT
                        //Here we are checking if character is not in crouch position and if he is not moving
                        if (crouch < 0.5f && sliding == false)
                        {
                            player_rigidbody2d.velocity = new Vector2(inputX * movingSpeed, player_rigidbody2d.velocity.y);
                        }
                        else if (sliding == false)
                        {
                            player_rigidbody2d.velocity = Vector2.zero;
                        }

                        if (player_grounded)
                        {
                            if (crouch >= 0.5f)
                            { //If crouch button is pressed we will play crouch animation (animation has on it event to turn off collider when playing it)
                                player_animator.SetInteger("AnimState", 2);
                                player_rigidbody2d.velocity = Vector2.zero;
                            }
                            else if (slide >= 0.5f)
                            { //If slide button is pressed we will play slide animation (animation has on it event to turn off collider when playing it)
                                if (canSlide == true && Time.time >= nextslideTime)
                                {
                                    player_animator.SetInteger("AnimState", 3);
                                    if (flipped)
                                    { //Also based on characters direction we will push him to slide
                                        player_rigidbody2d.velocity = new Vector2(-slideForce, player_rigidbody2d.velocity.y);
                                    }
                                    else
                                    {
                                        player_rigidbody2d.velocity = new Vector2(slideForce, player_rigidbody2d.velocity.y);
                                    }
                                    sfxPlayer.PlaySFX(AudioClipType.Slide);
                                    sliding = true;
                                    nextslideTime = Time.time + 1f / slideRate;
                                }
                                slide = 0f; //Reset slide button press
                            }
                            else if (!sliding)
                            {
                                if (player_rigidbody2d.velocity.magnitude != 0f)
                                {
                                    player_animator.SetInteger("AnimState", 1);
                                }
                                else
                                {
                                    player_animator.SetInteger("AnimState", 0);
                                }
                            }
                        }
                        else
                        {
                            crouch = 0f;
                            slide = 0f;
                        }

                        if (canJump == true && !sliding)
                        { //Here we are setting condition for jump function
                            if (jump == 1f && jumpCount < 2)
                            { //jumpCount variable is used to determine if we can double jump
                                player_animator.SetTrigger("Jump");
                                player_rigidbody2d.velocity = new Vector2(player_rigidbody2d.velocity.x, jumpingForce);
                                jumpCount++;

                                sfxPlayer.PlaySFX(AudioClipType.Jump);
                            }

                            jump = 0f;
                        }
                        else
                        {
                            jump = 0f;
                        }
                        #endregion

                        #region COMBAT
                        if (player_rigidbody2d.velocity.magnitude != 0f && !player_grounded)
                        { //If we are moving on X axis and we are on ground GROUNDED
                            if (canUseSword && attack == 1f)
                            {
                                if (Time.time >= nextAttackTime)
                                {
                                    player_animator.SetFloat("AttackType", Random.Range(0f, 2f));
                                    player_animator.SetTrigger("Attack");
                                    sfxPlayer.PlaySFX(AudioClipType.Swing);
                                    nextAttackTime = Time.time + 1f / meleeRate;
                                }
                            }
                            else if (canUseBow && fire_bow == 1f)
                            {
                                player_animator.SetTrigger("Shoot");
                                fire_bow = 0f;
                            }
                            else if (canUseSword && slamAttack >= 0.5f)
                            {
                                player_animator.SetBool("AirSlamReady", true);
                            }
                        }
                        else if (player_rigidbody2d.velocity.magnitude == 0f && player_grounded)
                        {
                            if (crouch < 0.5f && block == 0f)
                            {
                                if (canUseSword && attack == 1f)
                                {
                                    if (Time.time >= nextAttackTime)
                                    {
                                        player_animator.SetFloat("AttackType", Random.Range(0f, 2f));
                                        player_animator.SetTrigger("Attack");
                                        sfxPlayer.PlaySFX(AudioClipType.Swing);
                                        nextAttackTime = Time.time + 1f / meleeRate;
                                    }
                                }
                                else if (canUseBow && fire_bow == 1f)
                                {
                                    player_animator.SetTrigger("Shoot");
                                    fire_bow = 0f;
                                }
                            }
                            player_animator.SetBool("AirSlamReady", false);
                        }
                        else
                        {
                            player_animator.SetBool("AirSlamReady", false);
                        }

                        if (canUseSword && block == 1f && crouch < 0.5f && player_rigidbody2d.velocity.magnitude == 0f)
                        {
                            player_animator.SetBool("Blocking", true);
                            blocking = true;
                        }
                        else
                        {
                            player_animator.SetBool("Blocking", false);
                            blocking = false;
                        }
                        #endregion
                    }
                    else
                    {
                        #region WATER MOVING
                        player_rigidbody2d.velocity = new Vector2(inputX * (movingSpeed * 0.75f), player_rigidbody2d.velocity.y);

                        if (player_rigidbody2d.velocity.magnitude != 0f)
                        {
                            player_animator.SetInteger("AnimState", 4);
                        }
                        else
                        {
                            player_animator.SetInteger("AnimState", 0);
                        }

                        if (jump == 1f)
                        {
                            player_animator.SetTrigger("Jump");
                            player_rigidbody2d.velocity = new Vector2(player_rigidbody2d.velocity.x, jumpingForce);
                            jump = 0f;
                        }
                        else
                        {
                            jump = 0f;
                        }
                        #endregion
                    }
                }
                else
                {
                    player_animator.SetInteger("AnimState", 0);
                    if (!resetMovement)
                    {
                        player_rigidbody2d.velocity = Vector2.zero;
                        resetMovement = true;
                    }
                }
                player_animator.SetInteger("JumpCount", jumpCount);
                player_animator.SetBool("Grounded", player_grounded);
            }
            else
            {
                if (jump == 1f)
                {
                    player_animator.SetTrigger("Jump");
                    player_rigidbody2d.velocity = new Vector2(player_rigidbody2d.velocity.x, jumpingForce);
                    ledgeDetector[2].transform.localPosition = Vector2.zero;
                    player_animator.SetBool("LedgeGrab", false);
                    jumpCount = 0;
                    player_rigidbody2d.gravityScale = 0.4f;
                    jump = 0f;
                    StartCoroutine(ResetLedge());
                }
            }
        }

        if (healthBar != null)
        {
            healthBar.fillAmount = (float)currentHealth / (float)maximumHealth;
        }

        if (sliding)
        {
            StartCoroutine(CheckSliding());
        }
        if (chat)
        {
            BuyItems();
        }


    }

    #region LEDGE GRABING AND SLIDING FUNCTIONS
    public void DetectLedge() {
        if (ledgeDetector != null) {

            verticalDetect = Physics2D.Raycast(ledgeDetector[0].position, -ledgeDetector[0].right, ledgeDetectDistance, groundLayer, ledgeDetectDistance);
            Debug.DrawRay(ledgeDetector[0].position, -ledgeDetector[0].right * ledgeDetectDistance, Color.blue);

            horizontalDetect = Physics2D.Raycast(ledgeDetector[1].position, -ledgeDetector[1].right, ledgeDetectDistance, groundLayer, ledgeDetectDistance);
            Debug.DrawRay(ledgeDetector[1].position, -ledgeDetector[1].right * ledgeDetectDistance, Color.yellow);

            downDetect = Physics2D.Raycast(transform.position, transform.up, ledgeDetectDistance * 3f, groundLayer, ledgeDetectDistance);
            Debug.DrawRay(transform.position, transform.up * (ledgeDetectDistance * 3F), Color.green);

            if (!horizontalDetect.collider && verticalDetect.collider && !downDetect) {
                StartCoroutine(GrabOnTheLedge());
            }
            else {
                ledgeDetector[2].transform.localPosition = Vector2.zero;
            }
        }
    }

    IEnumerator GrabOnTheLedge() {
        if (!onLedge) {
            findPointRaycast = Physics2D.Raycast(ledgeDetector[2].position, -ledgeDetector[2].up, ledgeDetectDistance, groundLayer, ledgeDetectDistance);
            Debug.DrawRay(ledgeDetector[2].position, -ledgeDetector[2].up * ledgeDetectDistance, Color.black);

            if (flipped) {
                ledgeDetector[2].transform.Translate(-ledgeDetector[2].right * Time.deltaTime * 1f, Space.Self);
            }
            else {
                ledgeDetector[2].transform.Translate(ledgeDetector[2].right * Time.deltaTime * 1f, Space.Self);
            }

            if (findPointRaycast) {
                ledgePos = findPointRaycast.point;
                ledgeDetector[2].transform.localPosition = Vector2.zero;

                if (flipped) {
                    var temp = new Vector2(ledgePos.x - ledgePosMod.x, ledgePos.y + ledgePosMod.y);
                    ledgePos = Vector2.zero;
                    transform.position = temp;
                }
                else {
                    var temp = new Vector2(ledgePos.x + ledgePosMod.x, ledgePos.y + ledgePosMod.y);
                    ledgePos = Vector2.zero;
                    transform.position = temp;
                }
                player_animator.SetBool("LedgeGrab", true);
                player_rigidbody2d.velocity = Vector2.zero;
                player_rigidbody2d.gravityScale = 0f;

                ledgeDetector[2].transform.localPosition = Vector2.zero;

                sfxPlayer.PlaySFX(AudioClipType.GroundImpact);

                onLedge = true;
            }
        }

        yield return new WaitForEndOfFrame();
    }

    IEnumerator ResetLedge() {
        yield return new WaitForSeconds(0.25f);
        onLedge = false;
    }
    public void ResetSliding() {
        sliding = false;
    }

    IEnumerator CheckSliding() {
        yield return new WaitForSeconds(0.35f);
        RaycastHit2D checkCelling = Physics2D.Raycast(transform.position, transform.up, 0.25f, groundLayer);
        Debug.DrawRay(transform.position, transform.up * 0.25f, Color.white);

        if (checkCelling.collider) {
            if (flipped) {
                player_rigidbody2d.velocity = new Vector2(-slideForce, player_rigidbody2d.velocity.y);
            }
            else {
                player_rigidbody2d.velocity = new Vector2(slideForce, player_rigidbody2d.velocity.y);
            }
        }

        StopCoroutine(CheckSliding());
    }

    #endregion


    #region ENABLERS AND DISABLERS
    public void EnableSlide() {
        canSlide = true;
    }

    public void DisableSlide() {
        canSlide = false;
    }

    public void EnableJump() {
        canJump = true;
    }

    public void DisableJump() {
        canJump = false;
    }

    public void EnableSword() {
        canUseSword = true;
    }

    public void DisableSword() {
        canUseSword = false;
    }

    public void EnableBow() {
        canUseBow = true;
    }

    public void DisableBow() {
        canUseBow = false;
    }

    public void OnChat()
    {
        Debug.Log("You made it");
    }


    public void SetPlayerEnableDisable(int state) {
        if (state == 0) {
            canControl = false;
        }
        else if (state == 1) {
            canControl = true;
            resetMovement = false;
        }
    }

    public void OnEnable() {
        input.Enable();
    }

    public void OnDisable() {
        input.Disable();
    }
    #endregion

    public void ShootArrow() {
        GameObject arrowGo = Instantiate(arrowPrefab, arrowPoint.position, transform.rotation);
        arrowGo.GetComponent<Rigidbody2D>().AddForce(transform.right * 150f);
        arrowGo.GetComponent<Arrow>().damage = arrowDamage;
        arrowGo.GetComponent<Arrow>().sfxPlayer = sfxPlayer;
    }

    public void HealthSet(int health) {
        currentHealth = health;
    }

    public void HealthAdd(int health) {
        currentHealth += health;
    }

    public void HealthRemove(int health) {
        currentHealth -= health;
    }

   public void AddCoin() {
        coinBalance += 1;
    }

    public void BuyItems()
    {
        Debug.Log("Congratulations");
        showStoreUi = true;
    }
    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction : FunctionMessage
    {
        [Parameter("address", "_owner", 1)] public string Owner { get; set; }
    }
    // This function is called every time another collider overlaps the trigger collider
     public async void OnTriggerEnter2D(Collider2D other)
    {
    
        // Checking if the overlapped collider is an enemy
        if (other.CompareTag("Coin")) {
          //  AddCoin();
           
            AddCoin();
            var balance = await Task.Run(coinhandler.GetBalance);
            Destroy(other.gameObject);
            Debug.Log(balance);

        }
        if (other.CompareTag("Finish") && did == false)
        {
            coinhandler.MintCoins();
            coinBalance = 0;
            did = true;
        }

        if (other.CompareTag("Merchant"))
        {
            Debug.Log("hurray");
            BuyItems();
        }

    }
}
