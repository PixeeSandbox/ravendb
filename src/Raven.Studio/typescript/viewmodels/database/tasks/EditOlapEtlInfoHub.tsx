﻿import AboutViewFloating, {AboutViewAnchored, AccordionItemWrapper} from "components/common/AboutView";
import AccordionLicenseNotIncluded from "components/common/AccordionLicenseNotIncluded";
import { licenseSelectors } from "components/common/shell/licenseSlice";
import { useAppSelector } from "components/store";
import React from "react";
import {Icon} from "components/common/Icon";
import {useRavenLink} from "hooks/useRavenLink";

export function EditOlapEtlInfoHub() {
    const isEnterpriseOrDeveloper = useAppSelector(licenseSelectors.isEnterpriseOrDeveloper());
    const olapEtlDocsLink = useRavenLink({ hash: "LYZL56" });

    return (
        <AboutViewFloating defaultOpen={!isEnterpriseOrDeveloper}>
            <AccordionItemWrapper
                targetId="about"
                icon="about"
                color="info"
                heading="About this view"
                description="Get additional info on this feature"
            >
                <p>
                    Text
                </p>
                <hr />
                <div className="small-label mb-2">useful links</div>
                <a href={olapEtlDocsLink} target="_blank">
                    <Icon icon="newtab" /> Docs - OLAP ETL
                </a>
            </AccordionItemWrapper>
            <AboutViewAnchored
                className="mt-2"
                defaultOpen={isEnterpriseOrDeveloper ? null : "licensing"}
            >
                <AccordionLicenseNotIncluded
                    targetId="licensing"
                    featureName="OLAP ETL"
                    featureIcon="olap-etl"
                    checkedLicenses={["Enterprise"]}
                    isLimited={!isEnterpriseOrDeveloper}
                />
            </AboutViewAnchored>
        </AboutViewFloating>
    );
}