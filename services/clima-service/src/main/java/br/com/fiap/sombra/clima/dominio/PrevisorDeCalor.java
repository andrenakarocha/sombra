package br.com.fiap.sombra.clima.dominio;

import java.util.List;

/**
 * Prevê o calor das próximas 24 h a partir da janela de observações das últimas 24 h. Duas implementações: o modelo de
 * ML exportado em ONNX (F9.1) e a {@link RegraPrevisorDeCalor}, o fallback que mantém o serviço útil sem o modelo.
 */
public interface PrevisorDeCalor {

    /**
     * @param janela as observações das últimas 24 h, da mais antiga à mais recente (pelo menos uma)
     * @return a previsão
     */
    PrevisaoDeCalor prever(List<Observacao> janela);
}
