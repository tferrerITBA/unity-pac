using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    public LevelManager levelManager;
    public Score score;
    public LivesManager livesManager;

    public Player player;
    public Ghost[] ghosts = new Ghost[4];

    private readonly List<Direction> _oppositeXDirections = new List<Direction>(
        new [] {Direction.Left, Direction.Right});
    private readonly List<Direction> _oppositeYDirections = new List<Direction>(
        new [] {Direction.Up, Direction.Down});

    public int fruitAppearanceTime;
    public int fruitDuration;
    public GameObject fruitBonus;
    public Sprite[] fruitBonusSprites;
    public Sprite bonusFruitScoreSprite;
    private int _fruitIndex;
    private float _fruitTimer;
    private FruitStatus _fruitStatus = FruitStatus.Waiting;

    private enum FruitStatus
    {
        Waiting = 0,
        Present = 1,
        Appeared = 2
    }

    private float _tileMapHalfWidth;

    public TMP_Text centerText;

    public ModeManager modeManager;
    private SoundManager soundManager;

    private const float WaitingTimeAfterReset = 2f;

    // Start is called before the first frame update
    void Start()
    {
        soundManager = GetComponent<SoundManager>();
        _tileMapHalfWidth = levelManager.TileMapHalfWidth;
        score.LivesManager = livesManager;
        
        IEnumerator coroutine = WaitForIntroMusic();
        StartCoroutine(coroutine);
    }

    private void Update()
    {
        // Debug.Log($"fruit timer {_fruitTimer} fruit status {_fruitStatus} fruit ");
        if (_fruitStatus == FruitStatus.Waiting)
        {
            _fruitTimer += Time.deltaTime;
            if (_fruitTimer > fruitAppearanceTime)
            {
                fruitBonus.SetActive(true);
                _fruitStatus = FruitStatus.Present;
                _fruitTimer = 0;
            }
        }
        else if (_fruitStatus == FruitStatus.Present)
        {
            _fruitTimer += Time.deltaTime;
            if (_fruitTimer > fruitDuration)
            {
                fruitBonus.SetActive(false);
                _fruitTimer = 0;
            }
        }
    }

    public Vector3 GetValidatedPosition(EntityId entityId, Vector3 position, Direction currentDirection, Direction? nextDirection)
    {
        return entityId == EntityId.Player ?
            levelManager.GetValidatedPlayerPosition(position, currentDirection, nextDirection) :
            levelManager.GetValidatedGhostPosition(entityId, position, currentDirection, nextDirection.GetValueOrDefault());
    }

    public void SetPlayerDirection(Direction direction)
    {
        player.CurrentDirection = direction;
        player.NextDirection = null;
        player.HasCollidedWall = false;
    }

    public Direction GetPlayerDirection()
    {
        return player.currentDirection;
    }

    public void ValidateInputDirection(Direction inputDirection, Direction currentDirection, bool hasCollidedWall)
    {
        levelManager.ValidateInputDirection(inputDirection, currentDirection, hasCollidedWall);
    }

    public void SetPlayerCollidedWall(bool hasCollided)
    {
        player.HasCollidedWall = hasCollided;
    }

    public void SetPlayerNextDirection(Direction nextDirection)
    {
        player.NextDirection = nextDirection;
    }

    public Vector2Int GetEntityTargetTileCoordinates(EntityId entityId)
    {
        return levelManager.GetEntityTargetTileCoordinates(entityId);
    }

    // Get new position based on direction, speed and frame delta time
    public Vector3 GetNewEntityPosition(float movSpeed,Vector2 position, Direction currentDirection)
    {
        Vector3 newPosition;
        float posX;
        switch (currentDirection)
        {
            case Direction.Left:
                posX = position.x - movSpeed * Time.deltaTime;
                newPosition = new Vector3(posX < -_tileMapHalfWidth ? _tileMapHalfWidth : posX, position.y, 0);
                break;
            case Direction.Right:
                posX = position.x + movSpeed * Time.deltaTime;
                newPosition = new Vector3(posX > _tileMapHalfWidth ? -_tileMapHalfWidth : posX, position.y, 0);
                break;
            case Direction.Up:
                newPosition = new Vector3(position.x, position.y + movSpeed * Time.deltaTime, 0);
                break;
            case Direction.Down:
                newPosition = new Vector3(position.x, position.y - movSpeed * Time.deltaTime, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return newPosition;
    }

    public bool DirectionsAreOpposite(Direction direction1, Direction direction2)
    {
        if (direction1 == direction2) return false;
        
        return (_oppositeXDirections.Contains(direction1) && _oppositeXDirections.Contains(direction2)) ||
               (_oppositeYDirections.Contains(direction1) && _oppositeYDirections.Contains(direction2));
    }

    public Vector2Int GetEntityCurrentTileCoordinates(EntityId entityId, Direction currentDirection)
    {
        return levelManager.GetEntityCurrentTileCoordinates(entityId, currentDirection);
    }

    public void DecrementLives()
    {
        int remainingLives = livesManager.DecrementLives();

        if (remainingLives == 0)
        {
            GameOver(false);
        }
        else
        {
            DisappearAndReset();
        }
    }

    public void GameOver(bool wonGame)
    {
        StopGhosts();
        player.gameObject.SetActive(false);
        if (wonGame)
        {
            centerText.text = "ROUND OVER!";
            centerText.color = Color.green;
            centerText.gameObject.SetActive(true);
            
            soundManager.PlayOutro();
            IEnumerator coroutine = WaitForNextRound();
            StartCoroutine(coroutine);
        }
        else
        {
            score.SaveScore("PAC");
            centerText.text = "GAME OVER";
            centerText.color = Color.red;
            centerText.gameObject.SetActive(true);
            IEnumerator coroutine = WaitForOutro();
            StartCoroutine(coroutine);
            //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1); TODO HIGHSCORE SCENE
        }
    }

    private IEnumerator WaitForNextRound()
    {
        Time.timeScale = 0;
        
        player.CanReadInput = false;
        
        fruitBonus.SetActive(false);
        _fruitStatus = FruitStatus.Appeared;
        _fruitTimer = 0;
        _fruitIndex = (_fruitIndex + 1) % fruitBonusSprites.Length;
        fruitBonus.GetComponent<SpriteRenderer>().sprite = fruitBonusSprites[_fruitIndex];

        var time = soundManager.GetOutroWaitTime();
        yield return new WaitForSecondsRealtime(time);
        
        centerText.text = "READY!";
        centerText.color = Color.yellow;
        
        levelManager.InitializeEntitiesProperties();
        levelManager.ResetPellets();
        modeManager.OnResetToNextRound();
        player.gameObject.SetActive(true);
        
        yield return new WaitForSecondsRealtime(WaitingTimeAfterReset);
        
        centerText.gameObject.SetActive(false);
        soundManager.PlaySiren();
        player.CanReadInput = true;
        
        _fruitStatus = FruitStatus.Waiting;
        
        Time.timeScale = 1;
    }

    private IEnumerator WaitForOutro()
    {
        Time.timeScale = 0;
        
        StopGhosts();
        soundManager.StopTileMapSound();
        var time = soundManager.GetDisappearingWaitTime();
        player.OnPauseGame(); // TODO NOT WORKING
        yield return new WaitForSeconds(time);
        
        time = soundManager.GetOutroWaitTime();
        yield return new WaitForSecondsRealtime(time);
        
        centerText.gameObject.SetActive(false);
        
        Time.timeScale = 1;
    }

    private void DisappearAndReset()
    {
        IEnumerator coroutine = WaitForDisappearing();
        StartCoroutine(coroutine);
    }
    
    private IEnumerator WaitForDisappearing()
    {
        StopGhosts();
        soundManager.StopTileMapSound();
        var time = soundManager.GetDisappearingWaitTime();
        player.OnPauseGame();
        yield return new WaitForSeconds(time);
        player.OnResumeGame();
        ResetPositions();
        centerText.gameObject.SetActive(true);
        IEnumerator coroutine = WaitAfterResetEntities();
        StartCoroutine(coroutine);
    }

    private IEnumerator WaitAfterResetEntities()
    {
        player.CanReadInput = false;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(WaitingTimeAfterReset);
        centerText.gameObject.SetActive(false);
        soundManager.PlaySiren();
        player.CanReadInput = true;
        Time.timeScale = 1;
    }
    
    private IEnumerator WaitForGhostConsumption(Ghost ghost)
    {
        player.OnPauseGameWhenEating();
        modeManager.OnPauseGameWhenEaten(ghost, player.EatenGhosts);
        var time = soundManager.GetConsumptionWaitTime();
        soundManager.PlayEatingGhostSound();
        yield return new WaitForSeconds(time);
        EatGhost(ghost);
        player.OnResumeGameWhenEating();
        modeManager.OnResumeGameWhenEaten(ghost);
    }

    private IEnumerator WaitForIntroMusic()
    {
        player.CanReadInput = false;
        Time.timeScale = 0;
        var time = soundManager.GetIntroWaitTime();
        soundManager.PlayIntro();
        yield return new WaitForSecondsRealtime(time);
        centerText.gameObject.SetActive(false);
        soundManager.PlaySiren();
        player.CanReadInput = true;
        Time.timeScale = 1;
    }

    private void StopGhosts()
    {
        modeManager.OnPauseGame();
    }

    private void StartGhosts()
    {
        modeManager.OnResumeGame();
    }

    public void SetFrightenedMode()
    {
        soundManager.PlayFrightenedMode();
        modeManager.SetFrightenedMode();
    }

    public void StopFrightenedMode()
    {
        player.ResetEatenGhosts();
        soundManager.PlaySiren();
    }

    private void ResetPositions()
    {
        levelManager.InitializeEntitiesProperties();
        StartGhosts();
    }

    public void CollideGhost(Ghost ghost)
    {
        if (ghost.currentState == Ghost.GhostState.Consumed) return;
            
        if (modeManager.currentMode == ModeManager.Mode.Frightened && !ghost.hasBeenEaten)
        {
            player.IncrementEatenGhost();
            AddEatenGhostPoints(player.EatenGhosts);
            IEnumerator coroutine = WaitForGhostConsumption(ghost);
            StartCoroutine(coroutine);
            return;
        }
        player.PlayDisappearingAnimation();
        DecrementLives();
    }

    public void EatGhost(Ghost ghost)
    {
        ghost.Consume();
        soundManager.PlayConsumedGhost();
    }

    public void AddEatenGhostPoints(int eatenGhosts)
    {
        score.AddEatenGhostPoints(eatenGhosts);
    }

    public void EatBonus(GameObject bonus)
    {
        score.AddFruitBonusPoints();
        
        IEnumerator coroutine = EatBonusFruitSprite(bonus);
        StartCoroutine(coroutine);
    }
    
    private IEnumerator EatBonusFruitSprite(GameObject bonusFruit)
    {
        var spriteRenderer = bonusFruit.GetComponent<SpriteRenderer>();
        var fruitCollider = bonusFruit.GetComponent<Collider2D>();
        fruitCollider.enabled = false;
        soundManager.PlayConsumedFruit();
        spriteRenderer.sprite = bonusFruitScoreSprite;
        yield return new WaitForSecondsRealtime(2);
        bonusFruit.SetActive(false);
        fruitCollider.enabled = true;
    }

    public void EatPellet(GameObject pelletGO, bool isPowerPellet)
    {
        if (isPowerPellet)
        {
            score.AddPowerPelletPoints();
            SetFrightenedMode();
        }
        else
        {
            score.AddPelletPoints();
        }
        
        pelletGO.SetActive(false);
        soundManager.PlayWakaWakaSound();
    }

    public void PlayFrightenedModeMelody()
    {
        soundManager.PlayFrightenedMode();
    }
}
