import React from "react";
import { Button, ButtonProps, Spinner } from "reactstrap";
import { Icon, IconProps } from "components/common/Icon";
import IconName from "typings/server/icons";
import classNames from "classnames";

interface ButtonWithSpinnerProps extends ButtonProps {
    isSpinning: boolean;
    icon?: IconName | IconProps;
    spinnerMargin?: string;
}

export default function ButtonWithSpinner(props: ButtonWithSpinnerProps) {
    const { isSpinning, icon, className, children, size, disabled, spinnerMargin, ...rest } = props;

    let IconElement: JSX.Element = null;

    if (icon) {
        IconElement = typeof icon === "string" ? <Icon icon={icon} /> : <Icon {...icon} />;
    }

    return (
        <Button
            className={classNames("d-flex", "align-items-center", className)}
            size={size}
            {...rest}
            disabled={disabled || isSpinning}
        >
            {isSpinning ? <Spinner size="sm" className={spinnerMargin ?? "me-1"} /> : IconElement}
            {children}
        </Button>
    );
}
