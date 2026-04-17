using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Менеджер погоды для 2D платформера.
/// Управляет типами погоды, плавными переходами и визуальными эффектами.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    [Header("Настройки погоды")]
    [SerializeField] private float transitionDuration = 10f; // Длительность плавного перехода в секундах

    [Header("Визуальные эффекты для каждого типа погоды")]
    [SerializeField] private GameObject rainEffectPrefab;
    [SerializeField] private GameObject snowEffectPrefab;
    [SerializeField] private GameObject fogEffectPrefab;
    // Для "ясно" эффекты не требуются

    [Header("Текущее состояние")]
    [SerializeField] private WeatherType currentWeather = WeatherType.Clear;

    private WeatherType targetWeather;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private float startIntensity = 1f;
    private float targetIntensity = 0f;

    private GameObject activeWeatherEffect;
    private ParticleSystem activeParticleSystem;
    private SpriteRenderer activeFogSprite;

    public static event Action<WeatherType> OnWeatherChanged;
    public static event Action<WeatherType> OnWeatherTransitionStarted;
    public static event Action<WeatherType> OnWeatherTransitionCompleted;

    public enum WeatherType
    {
        Clear,
        Rain,
        Snow,
        Fog
    }

    private void Awake()
    {
        // Singleton pattern (опционально)
        if (FindObjectsOfType<WeatherManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ApplyWeatherEffect(currentWeather, 1f);
    }

    /// <summary>
    /// Запускает плавный переход к новому типу погоды.
    /// </summary>
    public void ChangeWeather(WeatherType newWeather)
    {
        if (newWeather == currentWeather || isTransitioning)
            return;

        targetWeather = newWeather;
        isTransitioning = true;
        transitionTimer = 0f;
        startIntensity = 1f;
        targetIntensity = 0f;

        OnWeatherTransitionStarted?.Invoke(targetWeather);

        StartCoroutine(TransitionCoroutine());
    }

    /// <summary>
    /// Мгновенно устанавливает погоду без перехода.
    /// </summary>
    public void SetWeatherImmediately(WeatherType newWeather)
    {
        StopAllCoroutines();
        isTransitioning = false;
        currentWeather = newWeather;
        ApplyWeatherEffect(currentWeather, 1f);
        OnWeatherChanged?.Invoke(currentWeather);
        OnWeatherTransitionCompleted?.Invoke(currentWeather);
    }

    private IEnumerator TransitionCoroutine()
    {
        // Фаза 1: Затухание текущего эффекта
        while (transitionTimer < transitionDuration / 2f)
        {
            transitionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(transitionTimer / (transitionDuration / 2f));
            float intensity = Mathf.Lerp(1f, 0f, progress);
            
            SetEffectIntensity(intensity);
            yield return null;
        }

        // Переключаем эффект на новый тип
        currentWeather = targetWeather;
        ApplyWeatherEffect(currentWeather, 0f);
        
        // Сбрасываем таймер для фазы появления
        transitionTimer = 0f;

        // Фаза 2: Появление нового эффекта
        while (transitionTimer < transitionDuration / 2f)
        {
            transitionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(transitionTimer / (transitionDuration / 2f));
            float intensity = Mathf.Lerp(0f, 1f, progress);
            
            SetEffectIntensity(intensity);
            yield return null;
        }

        // Завершение перехода
        SetEffectIntensity(1f);
        isTransitioning = false;
        
        OnWeatherChanged?.Invoke(currentWeather);
        OnWeatherTransitionCompleted?.Invoke(currentWeather);
    }

    /// <summary>
    /// Применяет визуальный эффект для указанного типа погоды.
    /// </summary>
    private void ApplyWeatherEffect(WeatherType weather, float initialIntensity)
    {
        // Удаляем предыдущий эффект
        if (activeWeatherEffect != null)
        {
            Destroy(activeWeatherEffect);
            activeWeatherEffect = null;
            activeParticleSystem = null;
            activeFogSprite = null;
        }

        switch (weather)
        {
            case WeatherType.Rain:
                if (rainEffectPrefab != null)
                {
                    activeWeatherEffect = Instantiate(rainEffectPrefab, transform);
                    activeParticleSystem = activeWeatherEffect.GetComponent<ParticleSystem>();
                }
                break;

            case WeatherType.Snow:
                if (snowEffectPrefab != null)
                {
                    activeWeatherEffect = Instantiate(snowEffectPrefab, transform);
                    activeParticleSystem = activeWeatherEffect.GetComponent<ParticleSystem>();
                }
                break;

            case WeatherType.Fog:
                if (fogEffectPrefab != null)
                {
                    activeWeatherEffect = Instantiate(fogEffectPrefab, transform);
                    activeFogSprite = activeWeatherEffect.GetComponent<SpriteRenderer>();
                }
                break;

            case WeatherType.Clear:
                // Нет эффекта для ясной погоды
                break;
        }

        // Устанавливаем начальную интенсивность
        SetEffectIntensity(initialIntensity);
    }

    /// <summary>
    /// Устанавливает интенсивность текущего погодного эффекта (прозрачность/эмиссия).
    /// </summary>
    private void SetEffectIntensity(float intensity)
    {
        if (activeParticleSystem != null)
        {
            var main = activeParticleSystem.main;
            // Модифицируем альфа-канал цвета или количество частиц
            // Здесь можно настроить под конкретные префабы
            var emission = activeParticleSystem.emission;
            emission.rateOverTime = Mathf.Lerp(0, GetBaseEmissionRate(), intensity);
        }
        else if (activeFogSprite != null)
        {
            Color color = activeFogSprite.color;
            color.a = intensity;
            activeFogSprite.color = color;
        }
    }

    private float GetBaseEmissionRate()
    {
        // Возвращает базовую скорость эмиссии для частиц
        // Можно сделать настраиваемой через Inspector
        return 50f;
    }

    /// <summary>
    /// Возвращает текущий тип погоды.
    /// </summary>
    public WeatherType GetCurrentWeather()
    {
        return currentWeather;
    }

    /// <summary>
    /// Проверяет, идет ли сейчас переход между погодой.
    /// </summary>
    public bool IsTransitioning()
    {
        return isTransitioning;
    }

    // Пример метода для вызова из других скриптов при смене погоды
    // Другие объекты могут подписаться на событие OnWeatherChanged
    private void OnApplicationQuit()
    {
        OnWeatherChanged = null;
        OnWeatherTransitionStarted = null;
        OnWeatherTransitionCompleted = null;
    }
}
