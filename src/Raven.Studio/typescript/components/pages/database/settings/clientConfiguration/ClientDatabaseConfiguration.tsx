import React, { useMemo } from "react";
import {
    Form,
    Col,
    Button,
    Card,
    Row,
    Spinner,
    Input,
    InputGroupText,
    InputGroup,
    UncontrolledPopover,
    Fade,
} from "reactstrap";
import { SubmitHandler, useForm } from "react-hook-form";
import { FormCheckbox, FormInput, FormSelect, FormSwitch } from "components/common/Form";
import { useServices } from "components/hooks/useServices";
import { useAsync, useAsyncCallback } from "react-async-hook";
import { LoadingView } from "components/common/LoadingView";
import { useAccessManager } from "hooks/useAccessManager";
import { LoadError } from "components/common/LoadError";
import database = require("models/resources/database");
import {
    ClientConfigurationFormData,
    clientConfigurationYupResolver,
} from "components/common/clientConfiguration/ClientConfigurationValidation";
import { Icon } from "components/common/Icon";
import appUrl = require("common/appUrl");
import ClientConfigurationUtils from "components/common/clientConfiguration/ClientConfigurationUtils";
import useClientConfigurationFormController from "components/common/clientConfiguration/useClientConfigurationFormController";
import { tryHandleSubmit } from "components/utils/common";
import useClientConfigurationPopovers from "components/common/clientConfiguration/useClientConfigurationPopovers";
import { PropSummary, PropSummaryItem, PropSummaryName, PropSummaryValue } from "components/common/PropSummary";
import classNames from "classnames";

interface ClientDatabaseConfigurationProps {
    db: database;
}

// TODO: show modal on exit intent if is dirty
export default function ClientDatabaseConfiguration({ db }: ClientDatabaseConfigurationProps) {
    const { manageServerService } = useServices();
    const asyncGetClientConfiguration = useAsyncCallback(manageServerService.getClientConfiguration);
    const asyncGetClientGlobalConfiguration = useAsync(manageServerService.getGlobalClientConfiguration, []);

    const { isClusterAdminOrClusterNode: canNavigateToServerSettings } = useAccessManager();

    const { handleSubmit, control, formState, setValue, reset } = useForm<ClientConfigurationFormData>({
        resolver: clientConfigurationYupResolver,
        mode: "all",
        defaultValues: async () =>
            ClientConfigurationUtils.mapToFormData(await asyncGetClientConfiguration.execute(db), false),
    });

    const formValues = useClientConfigurationFormController(control, setValue);

    const globalConfig = useMemo(() => {
        const globalConfigResult = asyncGetClientGlobalConfiguration.result;
        if (!globalConfigResult) {
            return null;
        }

        return ClientConfigurationUtils.mapToFormData(globalConfigResult, true);
    }, [asyncGetClientGlobalConfiguration.result]);

    const onSave: SubmitHandler<ClientConfigurationFormData> = async (formData) => {
        tryHandleSubmit(async () => {
            await manageServerService.saveClientConfiguration(ClientConfigurationUtils.mapToDto(formData, false), db);
            reset(null, { keepValues: true });
        });
    };

    const onRefresh = async () => {
        reset(ClientConfigurationUtils.mapToFormData(await asyncGetClientConfiguration.execute(db), false));
    };

    if (asyncGetClientConfiguration.loading || asyncGetClientGlobalConfiguration.loading) {
        return <LoadingView />;
    }

    if (asyncGetClientConfiguration.error) {
        return <LoadError error="Unable to load client configuration" refresh={onRefresh} />;
    }

    const canEditDatabaseConfig = formValues.overrideConfig || !globalConfig;

    return (
        <Form onSubmit={handleSubmit(onSave)} autoComplete="off">
            <div className="content-margin">
                <Row>
                    <Col xxl={globalConfig ? 9 : 6}>
                        <div className="d-flex align-items-center justify-content-between flex-wrap gap-3 mb-3">
                            <div>
                                <Button
                                    type="submit"
                                    color="primary"
                                    disabled={formState.isSubmitting || !formState.isDirty}
                                >
                                    {formState.isSubmitting ? (
                                        <Spinner size="sm" className="me-1" />
                                    ) : (
                                        <i className="icon-save me-1" />
                                    )}
                                    Save
                                </Button>

                                {globalConfig && (
                                    <>
                                        <span id="EffectiveConfiguration" className="ms-1 p-2 cursor-pointer text-info">
                                            <Icon icon="config" />
                                            See effective configuration
                                        </span>

                                        <UncontrolledPopover
                                            target="EffectiveConfiguration"
                                            placement="bottom"
                                            trigger="hover"
                                            container="PopoverContainer"
                                        >
                                            <div className="bs5">
                                                <h5 className="px-3 mt-3 mb-2 text-center">
                                                    Effective configuration for
                                                </h5>
                                                <div className="text-primary my-2 text-center">
                                                    <Icon icon="database" />
                                                    {db.name}
                                                </div>
                                                <PropSummary className="pb-2">
                                                    <PropSummaryItem className="border-0">
                                                        <PropSummaryName>Identity parts separator</PropSummaryName>
                                                        <PropSummaryValue color="info">
                                                            {getIdentityPartsSeparatorEffectiveValue(
                                                                formValues,
                                                                globalConfig
                                                            )}
                                                        </PropSummaryValue>
                                                    </PropSummaryItem>
                                                    <PropSummaryItem>
                                                        <PropSummaryName>
                                                            Max number of requests per session
                                                        </PropSummaryName>
                                                        <PropSummaryValue color="info">
                                                            {getMaximumNumberOfRequestsEffectiveValue(
                                                                formValues,
                                                                globalConfig
                                                            )}
                                                        </PropSummaryValue>
                                                    </PropSummaryItem>
                                                    <PropSummaryItem>
                                                        <PropSummaryName>Load Balance Behavior</PropSummaryName>
                                                        <PropSummaryValue color="info">
                                                            {getLoadBalancerEffectiveValue(formValues, globalConfig)}
                                                        </PropSummaryValue>
                                                    </PropSummaryItem>
                                                    <PropSummaryItem>
                                                        <PropSummaryName>Seed</PropSummaryName>
                                                        <PropSummaryValue color="info">
                                                            {getLoadBalancerSeedEffectiveValue(
                                                                formValues,
                                                                globalConfig
                                                            )}
                                                        </PropSummaryValue>
                                                    </PropSummaryItem>
                                                    <PropSummaryItem>
                                                        <PropSummaryName>Read Balance Behavior</PropSummaryName>
                                                        <PropSummaryValue color="info">
                                                            {getReadBalanceBehaviorEffectiveValue(
                                                                formValues,
                                                                globalConfig
                                                            )}
                                                        </PropSummaryValue>
                                                    </PropSummaryItem>
                                                </PropSummary>
                                            </div>
                                        </UncontrolledPopover>
                                    </>
                                )}
                            </div>

                            {canNavigateToServerSettings() && (
                                <small title="Navigate to the server-wide Client Configuration View">
                                    <a target="_blank" href={appUrl.forGlobalClientConfiguration()}>
                                        <Icon icon="link" />
                                        Go to Server-Wide Client Configuration View
                                    </a>
                                </small>
                            )}
                        </div>

                        {globalConfig && (
                            <Row className="flex-grow-1 mt-4 mb-3">
                                <FormSwitch
                                    control={control}
                                    name="overrideConfig"
                                    color="primary"
                                    className="mt-1 mb-3"
                                >
                                    Override server configuration
                                </FormSwitch>
                                <Col>
                                    <div className="flex-horizontal gap-1">
                                        <h3 className="mb-0">
                                            <Icon icon="server" />
                                            Server Configuration
                                        </h3>
                                        {canNavigateToServerSettings() && (
                                            <a
                                                target="_blank"
                                                href={appUrl.forGlobalClientConfiguration()}
                                                className="me-1 no-decor"
                                                title="Server settings"
                                            >
                                                <Icon icon="link" />
                                            </a>
                                        )}
                                    </div>
                                </Col>
                                <Col>
                                    <h3 className="mb-0">
                                        <Icon icon="database" />
                                        Database Configuration
                                    </h3>
                                </Col>
                            </Row>
                        )}

                        <Card className="card flex-column p-3 mb-3">
                            <div
                                className={classNames("d-flex flex-grow-1", { "justify-content-center": globalConfig })}
                            >
                                <div className="md-label">
                                    Identity parts separator{" "}
                                    <Icon id="SetIdentityPartsSeparator" icon="info" color="info" />
                                </div>
                                <UncontrolledPopover
                                    target="SetIdentityPartsSeparator"
                                    trigger="hover"
                                    container="PopoverContainer"
                                    placement="top"
                                >
                                    <div className="p-3">
                                        Set the default separator for automatically generated document identity IDs.
                                        <br />
                                        Use any character except <code>&apos;|&apos;</code> (pipe).
                                    </div>
                                </UncontrolledPopover>
                            </div>
                            <Row className="flex-grow-1 align-items-start">
                                {globalConfig && (
                                    <>
                                        <Col className="d-flex">
                                            <Input
                                                defaultValue={globalConfig.identityPartsSeparatorValue}
                                                disabled
                                                placeholder={
                                                    globalConfig.identityPartsSeparatorValue || "'/' (default)"
                                                }
                                            />
                                        </Col>

                                        <GlobalSettingsSeparator
                                            active={
                                                formValues.overrideConfig && formValues.identityPartsSeparatorEnabled
                                            }
                                        />
                                    </>
                                )}
                                <Col className="d-flex">
                                    <InputGroup>
                                        <InputGroupText>
                                            <FormCheckbox
                                                control={control}
                                                name="identityPartsSeparatorEnabled"
                                                disabled={!canEditDatabaseConfig}
                                                color="primary"
                                            />
                                        </InputGroupText>
                                        <FormInput
                                            type="text"
                                            control={control}
                                            name="identityPartsSeparatorValue"
                                            placeholder="'/' (default)"
                                            disabled={
                                                !formValues.identityPartsSeparatorEnabled || !canEditDatabaseConfig
                                            }
                                            className="d-flex"
                                        />
                                    </InputGroup>
                                </Col>
                            </Row>
                        </Card>

                        <Card className="flex-column mb-3 p-3">
                            <div
                                className={classNames("d-flex flex-grow-1", { "justify-content-center": globalConfig })}
                            >
                                <div className="md-label">
                                    Maximum number of requests per session{" "}
                                    <Icon id="SetMaximumRequestsPerSession" icon="info" color="info" />
                                </div>
                                <UncontrolledPopover
                                    target="SetMaximumRequestsPerSession"
                                    trigger="hover"
                                    container="PopoverContainer"
                                    placement="top"
                                >
                                    <div className="p-3">
                                        Set this number to restrict the number of requests (<code>Reads</code> &{" "}
                                        <code>Writes</code>) per session in the client API.
                                    </div>
                                </UncontrolledPopover>
                            </div>
                            <Row className="flex-grow-1 align-items-start">
                                {globalConfig && (
                                    <>
                                        <Col className="d-flex">
                                            <Input
                                                defaultValue={globalConfig.maximumNumberOfRequestsValue}
                                                disabled
                                                placeholder={
                                                    globalConfig.maximumNumberOfRequestsValue
                                                        ? globalConfig.maximumNumberOfRequestsValue.toLocaleString()
                                                        : "30 (default)"
                                                }
                                            />
                                        </Col>

                                        <GlobalSettingsSeparator
                                            active={
                                                formValues.overrideConfig && formValues.maximumNumberOfRequestsEnabled
                                            }
                                        />
                                    </>
                                )}
                                <Col className="d-flex">
                                    <InputGroup>
                                        <InputGroupText>
                                            <FormCheckbox
                                                control={control}
                                                name="maximumNumberOfRequestsEnabled"
                                                disabled={!canEditDatabaseConfig}
                                                color="primary"
                                            />
                                        </InputGroupText>
                                        <FormInput
                                            type="number"
                                            control={control}
                                            name="maximumNumberOfRequestsValue"
                                            placeholder="30 (default)"
                                            disabled={
                                                !formValues.maximumNumberOfRequestsEnabled || !canEditDatabaseConfig
                                            }
                                        />
                                    </InputGroup>
                                </Col>
                            </Row>
                        </Card>
                        <div
                            className={classNames(
                                "d-flex mt-4 position-relative",
                                { "justify-content-center": globalConfig },
                                { "justify-content-between": !globalConfig }
                            )}
                        >
                            <h4 className={globalConfig && "text-center"}>Load Balancing Client Requests</h4>
                            <small title="Navigate to the documentation" className="position-absolute end-0">
                                <a href="https://ravendb.net/l/GYJ8JA/latest/csharp" target="_blank">
                                    <Icon icon="link" /> Load balancing tutorial
                                </a>
                            </small>
                        </div>
                        <Card className="flex-column p-3">
                            <div
                                className={classNames("d-flex flex-grow-1", { "justify-content-center": globalConfig })}
                            >
                                <div className="md-label">
                                    Load Balance Behavior <Icon id="SetSessionContext" icon="info" color="info" />
                                    <UncontrolledPopover
                                        target="SetSessionContext"
                                        trigger="hover"
                                        container="PopoverContainer"
                                        placement="top"
                                    >
                                        <div className="p-3">
                                            <span className="d-inline-block mb-1">
                                                Set the Load balance method for <strong>Read</strong> &{" "}
                                                <strong>Write</strong> requests.
                                            </span>
                                            <ul>
                                                <li className="mb-1">
                                                    <code>None</code>
                                                    <br />
                                                    Read requests - the node the client will target will be based the
                                                    Read balance behavior configuration.
                                                    <br />
                                                    Write requests - will be sent to the preferred node.
                                                </li>
                                                <li className="mb-1">
                                                    <code>Use session context</code>
                                                    <br />
                                                    Sessions that are assigned the same context will have all their Read
                                                    & Write requests routed to the same node.
                                                    <br />
                                                    The session context is hashed from a context string (given by the
                                                    client) and an optional seed.
                                                </li>
                                            </ul>
                                        </div>
                                    </UncontrolledPopover>
                                </div>
                            </div>
                            <Row className="mb-4 align-items-start">
                                {globalConfig && (
                                    <>
                                        <Col className="d-flex">
                                            <Input
                                                defaultValue={globalConfig.loadBalancerValue}
                                                disabled
                                                placeholder="None"
                                            />
                                        </Col>

                                        <GlobalSettingsSeparator
                                            active={formValues.overrideConfig && formValues.loadBalancerEnabled}
                                        />
                                    </>
                                )}
                                <Col className="d-flex align-items-center gap-3">
                                    <InputGroup>
                                        <InputGroupText>
                                            <FormCheckbox
                                                control={control}
                                                name="loadBalancerEnabled"
                                                disabled={!canEditDatabaseConfig}
                                                color="primary"
                                            />
                                        </InputGroupText>
                                        <FormSelect
                                            control={control}
                                            name="loadBalancerValue"
                                            disabled={!formValues.loadBalancerEnabled || !canEditDatabaseConfig}
                                            options={ClientConfigurationUtils.getLoadBalanceBehaviorOptions()}
                                        />
                                    </InputGroup>
                                </Col>
                            </Row>
                            {(globalConfig?.loadBalancerSeedValue ||
                                formValues.loadBalancerValue === "UseSessionContext") && (
                                <>
                                    <div
                                        className={
                                            globalConfig
                                                ? "d-flex flex-grow-1 justify-content-center"
                                                : "d-flex flex-grow-1"
                                        }
                                    >
                                        <div className="md-label">
                                            {" "}
                                            Seed
                                            <Icon id="SetLoadBalanceSeedBehavior" icon="info" color="info" />
                                            <UncontrolledPopover
                                                target="SetLoadBalanceSeedBehavior"
                                                trigger="hover"
                                                container="PopoverContainer"
                                                placement="top"
                                            >
                                                <div className="p-3">
                                                    An optional seed number.
                                                    <br />
                                                    Used when hashing the session context.
                                                </div>
                                            </UncontrolledPopover>
                                        </div>
                                    </div>
                                    <Row className="mb-4 align-items-start">
                                        {globalConfig && (
                                            <>
                                                <Col className="d-flex">
                                                    <Input
                                                        defaultValue={globalConfig.loadBalancerSeedValue}
                                                        disabled
                                                        placeholder="0 (default)"
                                                    />
                                                </Col>

                                                <GlobalSettingsSeparator
                                                    active={
                                                        formValues.overrideConfig && formValues.loadBalancerSeedEnabled
                                                    }
                                                />
                                            </>
                                        )}
                                        <Col className="d-flex align-items-center gap-3">
                                            <FormSwitch
                                                control={control}
                                                name="loadBalancerSeedEnabled"
                                                color="primary"
                                                label="Seed"
                                                disabled={
                                                    formValues.loadBalancerValue !== "UseSessionContext" ||
                                                    !canEditDatabaseConfig
                                                }
                                                className="small"
                                            ></FormSwitch>
                                            <InputGroup>
                                                <FormInput
                                                    type="number"
                                                    control={control}
                                                    name="loadBalancerSeedValue"
                                                    placeholder="0 (default)"
                                                    disabled={
                                                        !formValues.loadBalancerSeedEnabled || !canEditDatabaseConfig
                                                    }
                                                />
                                            </InputGroup>
                                        </Col>
                                    </Row>
                                </>
                            )}
                            <div
                                className={classNames("d-flex flex-grow-1", { "justify-content-center": globalConfig })}
                            >
                                <div className="md-label">
                                    Read Balance Behavior <Icon id="SetReadBalanceBehavior" icon="info" color="info" />
                                    <UncontrolledPopover
                                        target="SetReadBalanceBehavior"
                                        trigger="hover"
                                        container="PopoverContainer"
                                        placement="top"
                                    >
                                        <div className="p-3">
                                            <div>
                                                Set the Read balance method the client will use when accessing a node
                                                with
                                                <code> Read</code> requests.
                                                <br />
                                                <code>Write</code> requests are sent to the preferred node.
                                            </div>
                                        </div>
                                    </UncontrolledPopover>
                                </div>
                            </div>
                            <Row className="align-items-start">
                                {globalConfig && (
                                    <>
                                        <Col className="d-flex">
                                            <Input
                                                defaultValue={globalConfig.readBalanceBehaviorValue}
                                                placeholder="None"
                                                disabled
                                            />
                                        </Col>

                                        <GlobalSettingsSeparator
                                            active={formValues.overrideConfig && formValues.readBalanceBehaviorEnabled}
                                        />
                                    </>
                                )}

                                <Col className="d-flex">
                                    <InputGroup>
                                        <InputGroupText>
                                            <FormCheckbox
                                                control={control}
                                                name="readBalanceBehaviorEnabled"
                                                disabled={!canEditDatabaseConfig}
                                                color="primary"
                                            />
                                        </InputGroupText>
                                        <FormSelect
                                            control={control}
                                            name="readBalanceBehaviorValue"
                                            disabled={!formValues.readBalanceBehaviorEnabled || !canEditDatabaseConfig}
                                            options={ClientConfigurationUtils.getReadBalanceBehaviorOptions()}
                                        />
                                    </InputGroup>
                                </Col>
                            </Row>
                        </Card>
                    </Col>
                </Row>
            </div>
            <div id="PopoverContainer"></div>
        </Form>
    );
}

function GlobalSettingsSeparator(props: { active: boolean }) {
    const { active } = props;
    return (
        <Fade in={active} className={classNames("align-self-center col-sm-auto")}>
            <Icon icon="arrow-right" margin="m-0" />
        </Fade>
    );
}

function getIdentityPartsSeparatorEffectiveValue(
    formValues: ClientConfigurationFormData,
    globalConfig: ClientConfigurationFormData
) {
    return (
        (formValues.overrideConfig && formValues.identityPartsSeparatorValue) ||
        globalConfig?.identityPartsSeparatorValue ||
        "'/' (Default)"
    );
}

function getMaximumNumberOfRequestsEffectiveValue(
    formValues: ClientConfigurationFormData,
    globalConfig: ClientConfigurationFormData
) {
    return (
        (formValues.overrideConfig && formValues.maximumNumberOfRequestsValue) ||
        globalConfig?.maximumNumberOfRequestsValue ||
        "30 (Default)"
    );
}

function getLoadBalancerEffectiveValue(
    formValues: ClientConfigurationFormData,
    globalConfig: ClientConfigurationFormData
) {
    return (
        (formValues.overrideConfig && formValues.loadBalancerEnabled && formValues.loadBalancerValue) ||
        (globalConfig?.loadBalancerEnabled && globalConfig?.loadBalancerValue) ||
        "None (Default)"
    );
}

function getLoadBalancerSeedEffectiveValue(
    formValues: ClientConfigurationFormData,
    globalConfig: ClientConfigurationFormData
) {
    if (formValues.overrideConfig && formValues.loadBalancerEnabled) {
        if (formValues.loadBalancerValue === "None") return "Not set";
        else if (formValues.loadBalancerSeedEnabled) {
            return formValues.loadBalancerSeedValue || "0 (Default)";
        }
    }
    return globalConfig?.loadBalancerSeedValue || "0 (Default)";
}

function getReadBalanceBehaviorEffectiveValue(
    formValues: ClientConfigurationFormData,
    globalConfig: ClientConfigurationFormData
) {
    return (
        (formValues.overrideConfig && formValues.readBalanceBehaviorEnabled && formValues.readBalanceBehaviorValue) ||
        (globalConfig?.readBalanceBehaviorEnabled && globalConfig?.readBalanceBehaviorValue) ||
        "None (Default)"
    );
}