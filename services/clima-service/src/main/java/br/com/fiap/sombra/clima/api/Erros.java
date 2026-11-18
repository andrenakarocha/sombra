package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.aplicacao.RecursoJaExiste;
import br.com.fiap.sombra.clima.aplicacao.RecursoNaoEncontrado;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import java.net.URI;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.lang.Nullable;
import org.springframework.web.HttpRequestMethodNotSupportedException;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.MissingServletRequestParameterException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.context.request.WebRequest;
import org.springframework.web.method.annotation.HandlerMethodValidationException;
import org.springframework.web.method.annotation.MethodArgumentTypeMismatchException;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;
import org.springframework.web.servlet.resource.NoResourceFoundException;

/**
 * Erros também são contrato: cada exceção vira um ProblemDetails (RFC 9457) previsível, em pt-BR, com o
 * {@code correlationId} para cruzar com o log. Nenhum stack trace sai na resposta.
 */
@RestControllerAdvice
public class Erros extends ResponseEntityExceptionHandler {

    /** Onde o consumidor lê o significado de cada erro. */
    public static final URI TIPO =
            URI.create("https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/clima-service.md#erros");

    private static final Logger LOG = LoggerFactory.getLogger(Erros.class);

    @ExceptionHandler(RegraViolada.class)
    ResponseEntity<Object> regraViolada(RegraViolada e, WebRequest request) {
        ProblemDetail corpo = ProblemDetail.forStatusAndDetail(HttpStatus.UNPROCESSABLE_ENTITY, e.getMessage());
        corpo.setProperty("regra", e.getRegra());
        return handleExceptionInternal(e, corpo, new HttpHeaders(), HttpStatus.UNPROCESSABLE_ENTITY, request);
    }

    @ExceptionHandler(RecursoNaoEncontrado.class)
    ResponseEntity<Object> naoEncontrado(RecursoNaoEncontrado e, WebRequest request) {
        ProblemDetail corpo = ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, e.getMessage());
        return handleExceptionInternal(e, corpo, new HttpHeaders(), HttpStatus.NOT_FOUND, request);
    }

    @ExceptionHandler(RecursoJaExiste.class)
    ResponseEntity<Object> jaExiste(RecursoJaExiste e, WebRequest request) {
        ProblemDetail corpo = ProblemDetail.forStatusAndDetail(HttpStatus.CONFLICT, e.getMessage());
        return handleExceptionInternal(e, corpo, new HttpHeaders(), HttpStatus.CONFLICT, request);
    }

    @ExceptionHandler(Exception.class)
    ResponseEntity<Object> inesperado(Exception e, WebRequest request) {
        LOG.atError()
                .addKeyValue("event", "requisicao.falhou")
                .addKeyValue("tipo", e.getClass().getName())
                .setCause(e)
                .log("Falha inesperada na requisição");
        ProblemDetail corpo = ProblemDetail.forStatusAndDetail(
                HttpStatus.INTERNAL_SERVER_ERROR, "Erro interno. Informe o código do erro ao suporte.");
        return handleExceptionInternal(e, corpo, new HttpHeaders(), HttpStatus.INTERNAL_SERVER_ERROR, request);
    }

    @Override
    protected ResponseEntity<Object> handleMethodArgumentNotValid(
            MethodArgumentNotValidException e, HttpHeaders headers, HttpStatusCode status, WebRequest request) {
        Map<String, List<String>> erros = new LinkedHashMap<>();
        e.getBindingResult().getFieldErrors().forEach(f -> erros
                .computeIfAbsent(f.getField(), k -> new java.util.ArrayList<>())
                .add(f.getDefaultMessage()));
        e.getBindingResult().getGlobalErrors().forEach(g -> erros
                .computeIfAbsent(g.getObjectName(), k -> new java.util.ArrayList<>())
                .add(g.getDefaultMessage()));
        ProblemDetail corpo = ProblemDetail.forStatusAndDetail(status, "Um ou mais campos são inválidos.");
        corpo.setProperty("errors", erros);
        return handleExceptionInternal(e, corpo, headers, status, request);
    }

    @Override
    protected ResponseEntity<Object> handleHandlerMethodValidationException(
            HandlerMethodValidationException e, HttpHeaders headers, HttpStatusCode status, WebRequest request) {
        Map<String, List<String>> erros = new LinkedHashMap<>();
        e.getParameterValidationResults().forEach(r -> erros.put(
                nomeDoParametro(r.getMethodParameter().getParameterName()),
                r.getResolvableErrors().stream().map(m -> String.valueOf(m.getDefaultMessage())).toList()));
        ProblemDetail corpo = ProblemDetail.forStatusAndDetail(HttpStatus.BAD_REQUEST, "Um ou mais parâmetros são inválidos.");
        corpo.setProperty("errors", erros);
        return handleExceptionInternal(e, corpo, headers, HttpStatus.BAD_REQUEST, request);
    }

    @Override
    protected ResponseEntity<Object> handleExceptionInternal(
            Exception e, @Nullable Object body, HttpHeaders headers, HttpStatusCode status, WebRequest request) {
        ProblemDetail corpo = body instanceof ProblemDetail p ? p : ProblemDetail.forStatus(status);
        corpo.setType(TIPO);
        corpo.setTitle(titulo(status));
        String detalhe = detalheEmPortugues(e);
        if (detalhe != null) {
            corpo.setDetail(detalhe);
        }
        String correlationId = MDC.get("correlationId");
        if (correlationId != null) {
            corpo.setProperty("correlationId", correlationId);
        }
        return super.handleExceptionInternal(e, corpo, headers, status, request);
    }

    private static String titulo(HttpStatusCode status) {
        return switch (status.value()) {
            case 400 -> "Requisição inválida";
            case 404 -> "Recurso não encontrado";
            case 405 -> "Método não permitido";
            case 406 -> "Formato de resposta não suportado";
            case 409 -> "Conflito";
            case 415 -> "Formato do corpo não suportado";
            case 422 -> "Regra de negócio violada";
            case 500 -> "Erro interno";
            default -> HttpStatus.resolve(status.value()) != null
                    ? HttpStatus.valueOf(status.value()).getReasonPhrase()
                    : "Erro";
        };
    }

    /** Troca a frase em inglês das exceções do framework por uma em pt-BR; as do Sombra já vêm em pt-BR. */
    @Nullable
    private static String detalheEmPortugues(Exception e) {
        return switch (e) {
            case HttpMessageNotReadableException ignored -> "O corpo da requisição não é um JSON válido para este recurso.";
            case MethodArgumentTypeMismatchException m -> "O parâmetro '" + m.getName() + "' tem um valor inválido.";
            case MissingServletRequestParameterException m -> "O parâmetro '" + m.getParameterName() + "' é obrigatório.";
            case HttpRequestMethodNotSupportedException m -> "O método " + m.getMethod() + " não é aceito nesta rota.";
            case NoResourceFoundException ignored -> "Rota não encontrada.";
            default -> null;
        };
    }

    private static String nomeDoParametro(@Nullable String nome) {
        return nome == null ? "parametro" : nome;
    }
}
