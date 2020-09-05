﻿using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class Ghost : MonoBehaviour, IEntity, IPauseable
{
    public enum Mode
    {
        Chase = 0,
        Scatter = 1,
        Frightened = 2,
        Consumed = 3,
    }

    /*
     * Iteration phases
     */
    public int scatterModeTimer1 = 7;
    public int chaseModeTimer1 = 20;
    public int scatterModeTimer2 = 7;
    public int chaseModeTimer2 = 20;
    public int scatterModeTimer3 = 7;
    public int chaseModeTimer3 = 20;
    public int scatterModeTimer4 = 7;
    public int modeChangeIteration = 1;
    
    /*
     * Timers
     */
    private float _modeChangeTimer;
    private float _frightenedModeTimer;
    private float _blinkTimer;
    private bool _frightenedModeIsWhite;

    /*
     * Durations
     */
    public float frightenedModeDuration = 10;
    public float startBlinkingAt = 7;
    public float blinkFrequency = 0.1f;
    
    /*
     * Speeds
     */
    public float movSpeed = 3.9f;
    public float frightenedModeSpeed = 3.9f;
    private float _previousSpeed;
    private float _movSpeedBackup;

    /*
     * Modes
     */
    public Mode currentMode = Mode.Scatter;
    private Mode _previousMode = Mode.Scatter;
    public EntityId entityId;
    public Vector2Int ownCornerTile;

    /*
     * References to other managers 
     */
    public GameManager gameManager;
    public LevelManager levelManager;
    
    private Animator _animator;
    
    /*
     * Start is called before the first frame update.
     */
    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    /*
     * Update is called once per frame
     */
    void Update()
    {
        ModeUpdate();
        Move();
    }
    
    /*
     * This function determines whether a mode needs to be changed or not (in that case calls ChangeMode())
     * Ghosts iterate doing the scatter-chase combination. After every chase period, iteration number is
     * incremented.
     */
    private void ModeUpdate()
    {
        _modeChangeTimer += Time.deltaTime;
        if (currentMode != Mode.Frightened)
        {
            switch (modeChangeIteration)
            {
                case 1:
                {
                    /*
                     * Checking if it's time to switch from scatter to chase
                     */
                    if (currentMode == Mode.Scatter && _modeChangeTimer > scatterModeTimer1)
                    {
                        ChangeMode(Mode.Chase);
                        _modeChangeTimer = 0;
                    }
                    /*
                     * Checking if it's time to switch from chase to scatter
                     */
                    if (currentMode == Mode.Chase && _modeChangeTimer > chaseModeTimer1)
                    {
                        modeChangeIteration = 2;
                        ChangeMode(Mode.Scatter);
                        _modeChangeTimer = 0;
                    }

                    break;
                }
                case 2:
                {
                    if (currentMode == Mode.Scatter && _modeChangeTimer > scatterModeTimer2)
                    {
                        ChangeMode(Mode.Chase);
                        _modeChangeTimer = 0;
                    }
                    if (currentMode == Mode.Chase && _modeChangeTimer > chaseModeTimer2)
                    {
                        modeChangeIteration = 3;
                        ChangeMode(Mode.Scatter);
                        _modeChangeTimer = 0;
                    }

                    break;
                }
                case 3:
                {
                    if (currentMode == Mode.Scatter && _modeChangeTimer > scatterModeTimer3)
                    {
                        ChangeMode(Mode.Chase);
                        _modeChangeTimer = 0;
                    }
                    if (currentMode == Mode.Chase && _modeChangeTimer > chaseModeTimer3)
                    {
                        modeChangeIteration = 4;
                        ChangeMode(Mode.Scatter);
                        _modeChangeTimer = 0;
                    }

                    break;
                }
                case 4:
                {
                    /*
                     * If we're in chase mode in the last iteration we're  in chase mode forever
                     */
                    if (currentMode == Mode.Scatter && _modeChangeTimer > scatterModeTimer4)
                    {
                        ChangeMode(Mode.Chase);
                        _modeChangeTimer = 0;
                    }

                    break;
                }
            }
        }
        /*
         * Handling frightened mode. If less than 3 seconds are left, ghosts start blinking (switching between blue and white)
         */
        else if (currentMode == Mode.Frightened)
        {
            _frightenedModeTimer += Time.deltaTime;
            
            if (_frightenedModeTimer > startBlinkingAt && !_animator.GetBool("FrightenedEnding"))
            {
                _animator.SetBool("FrightenedEnding", true);
            }

            if (_frightenedModeTimer > frightenedModeDuration)
            {
                _animator.SetBool("Frightened", false);
                _animator.SetBool("FrightenedEnding", false);
                _frightenedModeTimer = 0;
                ChangeMode(_previousMode);
            }
        }
        
    }

    public void SetFrightenedMode()
    {
        _animator.SetBool("Frightened", true);
        _frightenedModeTimer = 0;
        ChangeMode(Mode.Frightened);
    }
    
    private void ChangeMode(Mode m)
    {
        if (currentMode == Mode.Frightened)
        {
            movSpeed = _previousSpeed;
        }

        if (m == Mode.Frightened)
        {
            _previousSpeed = movSpeed;
            movSpeed = frightenedModeSpeed;
        }

        Debug.Log($"Param: {m}; Previous: {_previousMode}; current: {currentMode}");
        if (m != currentMode)
        {
            _previousMode = currentMode;
            currentMode = m;
            Debug.Log($"DENTRO {currentMode} {_previousMode}");
        }
    }

    private void Move()
    {
        Vector3 newPosition = gameManager.GetNewEntityPosition(movSpeed, transform.position, currentDirection);
        if (levelManager.ReachedTargetTile(entityId, newPosition, currentDirection))
        {
            levelManager.UpdateTargetTile(entityId, currentDirection);
            var chosenDirection = ChooseNewDirection();
            transform.position = gameManager.GetValidatedPosition(entityId, newPosition, currentDirection, chosenDirection);
            currentDirection = chosenDirection;
            _animator.SetInteger("Direction", (int)currentDirection);
        }
        else
        {
            transform.position = newPosition;
        }
    }

    /*
     * Iterating through the nodes to see which is closer to targetTile (Pac-man)
     */
    private Direction ChooseNewDirection()
    {
        Direction chosenDirection = currentDirection; // Dummy value
        var currentTile = gameManager.GetEntityCurrentTileCoordinates(entityId, currentDirection);
        
        var validDirections = levelManager.GetValidDirectionsForTile(currentTile);
        if (validDirections.Count == 1)
        {
            return validDirections[0];
        }
        
        switch (currentMode)
        {
            case Mode.Chase:
                Vector2Int targetTile = ChooseTargetTile();
                chosenDirection = ChooseDirection(currentTile, targetTile, validDirections);
                break;
            case Mode.Scatter:
                chosenDirection = ChooseDirection(currentTile, ownCornerTile, validDirections);
                break;
            case Mode.Frightened:
                var randomIndex = Random.Range(0, validDirections.Count);
                chosenDirection = validDirections[randomIndex];
                break;
            case Mode.Consumed:
                //setear el home tile a la jaula
                var homeTile = new Vector2Int(0,0);
                chosenDirection = ChooseDirection(currentTile, homeTile, validDirections);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        

        return chosenDirection;
    }

    private Direction ChooseDirection(Vector2Int currentTile, Vector2Int targetTile, List<Direction> validDirections)
    {
        Direction chosenDirection = currentDirection; // Dummy value
        var leastDistance = float.MaxValue;
        
        foreach (var direction in validDirections)
        {
            if(gameManager.DirectionsAreOpposite(currentDirection, direction))
                continue;
            
            var xCoord = currentTile.x;
            var yCoord = currentTile.y;
            switch (direction)
            {
                case Direction.Down:
                    yCoord += 1;
                    break;
                case Direction.Up:
                    yCoord -= 1;
                    break;
                case Direction.Right:
                    xCoord += 1;
                    break;
                case Direction.Left:
                    xCoord -= 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            Vector2Int projectedTile = new Vector2Int(xCoord, yCoord);
            var distance = Vector2Int.Distance(targetTile, projectedTile);
            if (distance < leastDistance)
            {
                chosenDirection = direction;
                leastDistance = distance;
            }
        }

        return chosenDirection;
    }

    private Vector2Int ChooseTargetTile()
    {
        var pacManTile = gameManager.GetEntityCurrentTileCoordinates(EntityId.Player, gameManager.GetPlayerDirection());
        switch (entityId)
        {
            case EntityId.Blinky:
                return ChooseBlinkyTile(pacManTile);
            case EntityId.Pinky:
                return ChoosePinkyTile(pacManTile);
            case EntityId.Inky:
                return ChooseInkyTile(pacManTile);
            case EntityId.Clyde:
                return ChooseClydeTile(pacManTile);
            default:
                return new Vector2Int(0,0);
        }
    }

    private Vector2Int ChooseBlinkyTile(Vector2Int pacManTile)
    {
        return pacManTile;
    }
    
    private Vector2Int ChoosePinkyTile(Vector2Int pacManTile)
    {
        var playerDirection = gameManager.GetPlayerDirection();
        var xTarget = pacManTile.x;
        var yTarget = pacManTile.y;
        switch (playerDirection)
        {
            case Direction.Down:
                yTarget += 4;
                break;
            case Direction.Up:
                yTarget += 4;
                break;
            case Direction.Left:
                xTarget -= 4;
                break;
            case Direction.Right:
                xTarget -= 4;
                break;
        }
        return new Vector2Int(xTarget, yTarget);
    }
    
    private Vector2Int ChooseInkyTile(Vector2Int pacManTile)
    {
        var playerDirection = gameManager.GetPlayerDirection();
        var xPivot = pacManTile.x;
        var yPivot = pacManTile.y;
        switch (playerDirection)
        {
            case Direction.Down:
                yPivot += 2;
                break;
            case Direction.Up:
                yPivot += 2;
                break;
            case Direction.Left:
                xPivot -= 2;
                break;
            case Direction.Right:
                xPivot -= 2;
                break;
        }
        Vector2Int blinkyPosition =  gameManager.GetEntityCurrentTileCoordinates(EntityId.Blinky, gameManager.GetPlayerDirection());
        var xDifference = (xPivot - blinkyPosition.x);
        var yDifference = (yPivot - blinkyPosition.y);
        var xTarget = xPivot + xDifference;
        var yTarget = yPivot + yDifference;
        
        return new Vector2Int(xTarget, yTarget);
    }

    private Vector2Int ChooseClydeTile(Vector2Int pacManTile)
    {
        var distance = Vector2Int.Distance(pacManTile,
            gameManager.GetEntityCurrentTileCoordinates(EntityId.Clyde, gameManager.GetPlayerDirection()));

        if (distance > 8)
        {
            return pacManTile;
        }
        else
        {
            return ownCornerTile;
        }
    }

    public Direction currentDirection { get; set; }

    public void OnPauseGame()
    {
        _movSpeedBackup = movSpeed;
        movSpeed = 0;
        // gameObject.SetActive(false);
    }

    public void OnResumeGame()
    {
        // gameObject.SetActive(true);
        movSpeed = _movSpeedBackup;
        
    }
}
