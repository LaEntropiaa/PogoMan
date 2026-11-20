using System;

// Punto de conexión con el futuro sistema de guardado.
// Todavía no existe guardado en el juego, así que "Continuar" queda oculto.
// Cuando exista, el sistema de guardado solo tiene que asignar estas dos cosas:
//   SaveGameProbe.HasSaveProvider = () => MiSistema.HayPartida();
//   SaveGameProbe.ContinueAction  = () => MiSistema.CargarPartida();
public static class SaveGameProbe
{
    public static Func<bool> HasSaveProvider;
    public static Action ContinueAction;

    public static bool HasSave => HasSaveProvider != null && HasSaveProvider();
}
